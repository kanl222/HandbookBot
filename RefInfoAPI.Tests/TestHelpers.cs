using System.Net.Http;
using Microsoft.Extensions.Options;
using RefInfoAPI.Host.Auth;

namespace RefInfoAPI.Tests;

internal sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => send(request, ct);
}

internal sealed class StubClientFactory(HttpMessageHandler handler) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new(handler, disposeHandler: false) { Timeout = TimeSpan.FromSeconds(3) };
}

internal static class TestJwt
{
    public const string Key = "test-only-signing-key-with-more-than-thirty-two-bytes";
    public static JwtService Create(string issuer = "test-issuer", string audience = "test-audience")
        => new(Options.Create(new JwtOptions { Key = Key, Issuer = issuer, Audience = audience }));
}
