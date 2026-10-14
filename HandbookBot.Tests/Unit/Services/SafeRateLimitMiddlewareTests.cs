using BotEngine.Core.Services;
using HandbookBot.Core.Middleware;
using HandbookBot.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace HandbookBot.Tests.Unit.Services;

public class SafeRateLimitMiddlewareTests
{
    [Fact]
    public async Task ConcurrentRequests_KeepTheLimitAndIsolatePlatforms()
    {
        var clock = new ManualTimeProvider();
        var limiter = Create(clock, maxMessages: 2);
        var platform = Substitute.For<IMessagingPlatform>();
        var accepted = 0;
        Task Invoke(string name) => limiter.InvokeAsync(new IncomingMessage("chat", "user", "", null, name),
            platform, (_, _, _) =>
            {
                Interlocked.Increment(ref accepted);
                return Task.CompletedTask;
            });

        await Task.WhenAll(Enumerable.Range(0, 100).Select(_ => Task.Run(() => Invoke("MAX"))))
            .WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(2, accepted);
        await Invoke("Telegram");
        Assert.Equal(3, accepted);
        clock.Advance(TimeSpan.FromSeconds(61));
        await Invoke("max");
        Assert.Equal(4, accepted);
    }

    [Fact]
    public async Task MoreThanTenThousandUsers_ConcurrentCleanupCompletesAndKeepsFreshLimits()
    {
        var clock = new ManualTimeProvider();
        var limiter = Create(clock, maxMessages: 1);
        var platform = Substitute.For<IMessagingPlatform>();
        var accepted = 0;
        Task Invoke(string user) => limiter.InvokeAsync(new IncomingMessage("chat", user, "", null, "Test"),
            platform, (_, _, _) =>
            {
                Interlocked.Increment(ref accepted);
                return Task.CompletedTask;
            });

        for (var i = 0; i < 10_100; i++)
            await Invoke($"user-{i}");

        await Task.WhenAll(Enumerable.Range(0, 128).Select(i => Task.Run(() => Invoke($"user-{i}"))))
            .WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(10_100, accepted);

        clock.Advance(TimeSpan.FromSeconds(61));
        await Task.WhenAll(Enumerable.Range(0, 128).Select(i => Task.Run(() => Invoke($"user-{i}"))))
            .WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(10_228, accepted);
        await Invoke("user-0");
        Assert.Equal(10_228, accepted);
    }

    private static SafeRateLimitMiddleware Create(TimeProvider clock, int maxMessages)
        => new(Options.Create(new RateLimitOptions
        {
            MaxMessages = maxMessages,
            WindowSeconds = 60,
            ThrottleMessage = null
        }), NullLogger<SafeRateLimitMiddleware>.Instance, clock);
}
