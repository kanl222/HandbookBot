using BotEngine.Core;
using HandbookBot.Core;
using HandbookBot.Core.Middleware;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace HandbookBot.Tests.Unit.Services;

public class MessageTimeoutMiddlewareTests
{
    private static MessageTimeoutMiddleware Create()
        => new(Options.Create(new MessageProcessingOptions { MessageTimeoutSeconds = 1 }),
            NullLogger<MessageTimeoutMiddleware>.Instance);

    [Fact]
    public void Registration_PlacesLimiterAndDeadlineBeforeUserGate()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddLogging();
        services.AddBotEngine();
        services.AddRateLimiting();
        services.AddHandbookMessagePipeline();
        using var provider = services.BuildServiceProvider();

        var middleware = provider.GetServices<IBotMiddleware>().Select(item => item.GetType()).ToArray();
        Assert.Equal(new[] { typeof(SafeRateLimitMiddleware), typeof(MessageTimeoutMiddleware), typeof(UserConcurrencyMiddleware) }, middleware);
        Assert.Equal(60, provider.GetRequiredService<IOptions<MessageProcessingOptions>>().Value.MessageTimeoutSeconds);
    }

    [Fact]
    public async Task Timeout_WaitsForOperationCleanupBeforeReleasingUserGate()
    {
        var timeout = Create();
        var gate = new UserConcurrencyMiddleware();
        var platform = Substitute.For<IMessagingPlatform>();
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cleanup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondStarted = false;
        var message = new IncomingMessage("chat", "user", "", null, "Test");
        var first = timeout.InvokeAsync(message, platform, (msg, plt, token) =>
            gate.InvokeAsync(msg, plt, async (_, _, ct) =>
            {
                try
                {
                    await Task.Delay(Timeout.Infinite, ct);
                }
                finally
                {
                    cancelled.TrySetResult();
                    await cleanup.Task;
                }
            }, token));
        Task? second = null;
        try
        {
            await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(first.IsCompleted);
            second = gate.InvokeAsync(message, platform, (_, _, _) =>
            {
                secondStarted = true;
                return Task.CompletedTask;
            });
            Assert.False(secondStarted);

            var otherCompleted = false;
            await gate.InvokeAsync(message with { UserId = "other" }, platform, (_, _, _) =>
            {
                otherCompleted = true;
                return Task.CompletedTask;
            });
            Assert.True(otherCompleted);
        }
        finally
        {
            cleanup.TrySetResult();
        }
        await first.WaitAsync(TimeSpan.FromSeconds(5));
        await second!.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(secondStarted);
        await platform.Received(1).SendTextAsync("chat",
            Arg.Is<string>(text => text != null && text.Contains("слишком много времени")),
            Arg.Any<BotKeyboard?>(), Arg.Is<CancellationToken>(token => token.CanBeCanceled));
    }

    [Fact]
    public async Task Timeout_IncludesWaitingForTheUserGate()
    {
        var gate = new UserConcurrencyMiddleware();
        var platform = Substitute.For<IMessagingPlatform>();
        var message = new IncomingMessage("chat", "user", "", null, "Test");
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = gate.InvokeAsync(message, platform, (_, _, _) => release.Task);
        var commandStarted = false;
        try
        {
            await Create().InvokeAsync(message, platform, (msg, plt, ct) =>
                gate.InvokeAsync(msg, plt, (_, _, _) =>
                {
                    commandStarted = true;
                    return Task.CompletedTask;
                }, ct)).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(commandStarted);
            Assert.False(first.IsCompleted);
        }
        finally
        {
            release.TrySetResult();
            await first;
        }

        await gate.InvokeAsync(message, platform, (_, _, _) =>
        {
            commandStarted = true;
            return Task.CompletedTask;
        });
        Assert.True(commandStarted);
    }

    [Fact]
    public async Task HostCancellation_PropagatesWithoutSendingTimeoutNotification()
    {
        using var stopping = new CancellationTokenSource();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var platform = Substitute.For<IMessagingPlatform>();
        var execution = Create().InvokeAsync(new IncomingMessage("chat", "user", "", null, "Test"),
            platform, (_, _, ct) =>
            {
                started.TrySetResult();
                return Task.Delay(Timeout.Infinite, ct);
            }, stopping.Token);
        await started.Task;
        stopping.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => execution);
        await platform.DidNotReceive().SendTextAsync(Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<BotKeyboard?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AdapterThatSwallowsCancellation_StillProducesTimeoutNotification()
    {
        var platform = Substitute.For<IMessagingPlatform>();
        await Create().InvokeAsync(new IncomingMessage("chat", "user", "", null, "Test"),
            platform, async (_, _, ct) =>
            {
                try { await Task.Delay(Timeout.Infinite, ct); }
                catch (OperationCanceledException) { }
            }).WaitAsync(TimeSpan.FromSeconds(5));

        await platform.Received(1).SendTextAsync(Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<BotKeyboard?>(), Arg.Any<CancellationToken>());
    }
}
