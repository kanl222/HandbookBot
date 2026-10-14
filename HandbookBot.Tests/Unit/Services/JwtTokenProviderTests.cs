using System.Net;
using System.Net.Http.Json;
using HandbookBot.Data.Api;
using HandbookBot.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HandbookBot.Tests.Unit.Services;

public class JwtTokenProviderTests
{
    [Fact]
    public async Task SimultaneousRefresh_UsesOneLogin()
    {
        var clock = new ManualTimeProvider();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        using var handler = new StubHttpMessageHandler(async (_, ct) =>
        {
            Interlocked.Increment(ref calls);
            started.TrySetResult();
            await release.Task.WaitAsync(ct);
            return TokenResponse("token", clock);
        });
        var provider = Create(handler, clock);
        var tasks = Enumerable.Range(0, 32).Select(_ => provider.GetTokenAsync()).ToArray();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        release.TrySetResult();
        var tokens = await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.All(tokens, token => Assert.Equal("token", token));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task FailedLogin_IsSharedDuringCooldownAndRecoversAfterFiveSeconds()
    {
        var clock = new ManualTimeProvider();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var succeed = false;
        using var handler = new StubHttpMessageHandler(async (_, ct) =>
        {
            Interlocked.Increment(ref calls);
            await release.Task.WaitAsync(ct);
            return succeed ? TokenResponse("recovered", clock) : new HttpResponseMessage(HttpStatusCode.Unauthorized);
        });
        var provider = Create(handler, clock);
        var tasks = Enumerable.Range(0, 32).Select(_ => provider.GetTokenAsync()).ToArray();
        release.TrySetResult();
        await Task.WhenAll(tasks.Select(task => Assert.ThrowsAsync<HttpRequestException>(async () => await task)))
            .WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, calls);
        clock.Advance(TimeSpan.FromSeconds(4));
        await Assert.ThrowsAsync<HttpRequestException>(() => provider.GetTokenAsync());
        Assert.Equal(1, calls);
        clock.Advance(TimeSpan.FromSeconds(2));
        succeed = true;
        Assert.Equal("recovered", await provider.GetTokenAsync());
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task CallerCancellation_DoesNotStartCooldownOrBlockTheNextLogin()
    {
        var clock = new ManualTimeProvider();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        using var handler = new StubHttpMessageHandler(async (_, ct) =>
        {
            if (Interlocked.Increment(ref calls) == 1)
            {
                started.TrySetResult();
                await Task.Delay(Timeout.Infinite, ct);
            }
            return TokenResponse("next", clock);
        });
        var provider = Create(handler, clock);
        using var cancelled = new CancellationTokenSource();
        var first = provider.GetTokenAsync(cancelled.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        Assert.Equal("next", await provider.GetTokenAsync());
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task LateUnauthorizedResponse_DoesNotInvalidateRefreshedToken()
    {
        var clock = new ManualTimeProvider();
        var loginCalls = 0;
        using var login = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(TokenResponse(Interlocked.Increment(ref loginCalls) == 1 ? "old" : "new", clock)));
        var provider = Create(login, clock);
        var secondStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var lateResponse = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var api = new StubHttpMessageHandler(async (request, ct) =>
        {
            if (request.Headers.Authorization?.Parameter == "old")
            {
                if (request.RequestUri!.AbsolutePath.EndsWith("/two"))
                {
                    secondStarted.TrySetResult();
                    await lateResponse.Task.WaitAsync(ct);
                }
                else
                {
                    await secondStarted.Task.WaitAsync(ct);
                }
                return new HttpResponseMessage(HttpStatusCode.Unauthorized);
            }
            Assert.Equal("new", request.Headers.Authorization?.Parameter);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var auth = new JwtAuthHandler(provider) { InnerHandler = api };
        using var client = new HttpClient(auth) { BaseAddress = new Uri("https://unit.test/"), Timeout = TimeSpan.FromSeconds(5) };
        var first = client.GetAsync("one");
        var second = client.GetAsync("two");
        try
        {
            using var firstResponse = await first.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        }
        finally
        {
            lateResponse.TrySetResult();
        }
        using var secondResponse = await second.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.Equal(2, loginCalls);
    }

    private static JwtTokenProvider Create(HttpMessageHandler handler, TimeProvider clock)
        => new(Options.Create(new RefInfoApiOptions
        {
            BaseUrl = "https://unit.test/",
            Username = "test",
            Password = "test"
        }), NullLogger<JwtTokenProvider>.Instance, new StubHttpClientFactory(handler), clock);

    private static HttpResponseMessage TokenResponse(string token, TimeProvider clock)
        => new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { accessToken = token, expiresAt = clock.GetUtcNow().AddHours(1).UtcDateTime })
        };
}
