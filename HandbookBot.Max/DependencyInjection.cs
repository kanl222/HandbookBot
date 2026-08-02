    using HandbookBot.Core.Interfaces;
    using MAX.Bot.Extensions;
    using MAX.Bot.Interfaces;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;

    namespace HandbookBot.Max;

    public static class DependencyInjection
    {
        public static IServiceCollection AddMaxPlatform(this IServiceCollection services, IConfiguration config)
        {
            var token = config["Max:Token"];
            if (string.IsNullOrWhiteSpace(token))
                throw new InvalidOperationException("Max:Token не задан. Добавьте его в appsettings.json или User Secrets.");

            var timeoutSeconds = config.GetValue("Max:TimeoutSeconds", 30);

            // Регистрируем адаптер как синглтон и для конкретного типа, и для интерфейса.
            services.AddSingleton<MaxPlatformAdapter>();
            services.AddSingleton<IMessagingPlatform>(sp => sp.GetRequiredService<MaxPlatformAdapter>());
            services.AddMaxBotClient(token, timeoutSeconds);
 
            services.AddHostedService<MaxPollingWorker>();

            return services;
        }
    }