using HandbookBot.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Telegram.Bot;

namespace HandbookBot.Telegram;

public static class DependencyInjection
{
    /// <summary>
    /// Регистрирует Telegram-адаптер, polling worker.
    /// Вызывается из Host/Program.cs.
    /// </summary>
    public static IServiceCollection AddTelegramPlatform(this IServiceCollection services, string token)
    {
        services.AddSingleton<ITelegramBotClient>(new TelegramBotClient(token));
        
        services.AddSingleton<TelegramPlatformAdapter>();
        services.AddSingleton<IMessagingPlatform>(sp => sp.GetRequiredService<TelegramPlatformAdapter>());
        services.AddHostedService<TelegramPollingWorker>();
        
        return services;
    }
}