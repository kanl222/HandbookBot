using BotEngine.Core.Services;
using HandbookBot.Core.Middleware;
using Microsoft.Extensions.DependencyInjection;

namespace HandbookBot.Core;

public static class DependencyInjection
{
    /// <summary>Вызывать после AddBotEngine. Лимитер предшествует ожиданию пользовательской очереди.</summary>
    public static IServiceCollection AddHandbookMessagePipeline(this IServiceCollection services)
    {
        // Заменяем только известные middleware, сохраняя остальные регистрации проекта.
        for (var i = services.Count - 1; i >= 0; i--)
        {
            var type = services[i].ImplementationType;
            if (services[i].ServiceType == typeof(IBotMiddleware) &&
                (type == typeof(RateLimitMiddleware) || type == typeof(UserConcurrencyMiddleware) ||
                 type == typeof(SafeRateLimitMiddleware) || type == typeof(MessageTimeoutMiddleware)))
                services.RemoveAt(i);
        }

        services.AddOptions<RateLimitOptions>()
            .BindConfiguration(RateLimitOptions.SectionName)
            .Validate(options => options.MaxMessages > 0, "MaxMessages должен быть больше нуля.")
            .Validate(options => options.WindowSeconds > 0, "WindowSeconds должен быть больше нуля.")
            .ValidateOnStart();
        services.AddOptions<MessageProcessingOptions>()
            .BindConfiguration(MessageProcessingOptions.SectionName)
            .Validate(options => options.MessageTimeoutSeconds > 0 &&
                                 options.MessageTimeoutSeconds <= 4_294_967,
                "MessageTimeoutSeconds должен быть положительным и поддерживаться таймером.")
            .ValidateOnStart();

        services.AddSingleton<IBotMiddleware, SafeRateLimitMiddleware>();
        services.AddSingleton<IBotMiddleware, MessageTimeoutMiddleware>();
        services.AddSingleton<IBotMiddleware, UserConcurrencyMiddleware>();
        return services;
    }
}
