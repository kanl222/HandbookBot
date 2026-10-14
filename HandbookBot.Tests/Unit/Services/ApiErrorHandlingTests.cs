using System.Net;
using System.Net.Http.Json;
using HandbookBot.Data;
using HandbookBot.Data.Api;
using HandbookBot.Tests.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace HandbookBot.Tests.Unit.Services;

public class ApiErrorHandlingTests
{
    [Theory]
    [InlineData("drugs", 401)]
    [InlineData("drugs", 403)]
    [InlineData("drugs", 500)]
    [InlineData("drugs", 503)]
    [InlineData("pharmacies", 401)]
    [InlineData("pharmacies", 403)]
    [InlineData("pharmacies", 500)]
    [InlineData("pharmacies", 503)]
    public async Task GetById_ApiErrorsAreNotReportedAsMissingItems(string repository, int code)
    {
        using var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage((HttpStatusCode)code)));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://unit.test/") };
        var error = await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            if (repository == "drugs")
                await new ApiPreparationRepository(http, NullLogger<ApiPreparationRepository>.Instance).GetByIdAsync(1);
            else
                await new ApiPharmacyRepository(http).GetByIdAsync(1);
        });
        Assert.Equal((HttpStatusCode)code, error.StatusCode);
    }

    [Theory]
    [InlineData("drugs")]
    [InlineData("pharmacies")]
    public async Task GetById_NotFoundReturnsNull(string repository)
    {
        using var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://unit.test/") };
        if (repository == "drugs")
            Assert.Null(await new ApiPreparationRepository(http, NullLogger<ApiPreparationRepository>.Instance).GetByIdAsync(1));
        else
            Assert.Null(await new ApiPharmacyRepository(http).GetByIdAsync(1));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AuthenticationUsesTheSameBaseUrlAsTheRepositories(bool modernConfiguration)
    {
        var config = new Dictionary<string, string?>
        {
            ["Data:Source"] = "api",
            ["Data:ApiBaseUrl"] = "https://legacy.unit.test/",
            ["RefInfoApi:Username"] = "test",
            ["RefInfoApi:Password"] = "test"
        };
        if (modernConfiguration)
            config["RefInfoApi:BaseUrl"] = "https://modern.unit.test/";
        Uri? loginUri = null;
        using var handler = new StubHttpMessageHandler((request, _) =>
        {
            loginUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { accessToken = "test-token", expiresAt = DateTime.UtcNow.AddHours(1) })
            });
        });
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(config).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHandbookData(configuration);
        services.AddSingleton<IHttpClientFactory>(new StubHttpClientFactory(handler));
        using var provider = services.BuildServiceProvider();

        Assert.Equal("test-token", await provider.GetRequiredService<JwtTokenProvider>().GetTokenAsync());
        Assert.Equal(modernConfiguration ? "https://modern.unit.test/auth/login" : "https://legacy.unit.test/auth/login",
            loginUri!.AbsoluteUri);
    }
}
