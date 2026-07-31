using HandbookBot.Core.Interfaces;
using MAX.Bot.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HandbookBot.Max;

public static class DependencyInjection
{
    public static IServiceCollection AddMaxPlatform(this IServiceCollection services, IConfiguration config)
    {
        var token = config["Max:Token"]
            ?? throw new InvalidOperationException("Max:Token не задан. Добавьте его в appsettings.json или User Secrets.");

        var timeoutSeconds = 30;
        if (int.TryParse(config["Max:TimeoutSeconds"], out var parsedTimeout))
        {
            timeoutSeconds = parsedTimeout;
        }

        services.AddSingleton<MaxPlatformAdapter>();
        services.AddSingleton<IMessagingPlatform>(sp => sp.GetRequiredService<MaxPlatformAdapter>());
        
        // Регистрируем IMaxBotClient руками, так как SDK не предоставляет перегрузку с IServiceProvider для токена
        services.AddHttpClient("MaxBot", (sp, client) =>
        {
            client.BaseAddress = new Uri(MAX.Bot.MaxBotClient.BaseUrl);
            
            if (!string.IsNullOrWhiteSpace(token))
            {
                client.DefaultRequestHeaders.Add("Authorization", token);
            }
            
            client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
        });

        services.AddSingleton<MAX.Bot.Interfaces.IMaxBotClient>(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient("MaxBot");
            return new MAX.Bot.MaxBotClient(token, httpClient);
        });

        services.AddHostedService<MaxPollingWorker>();

        return services;
    }
}