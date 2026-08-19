using HandbookBot.Core.Commands;
using HandbookBot.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace HandbookBot;

public static class CommandsRegistration
{
    /// <summary>
    /// Регистрирует все команды бота через keyed DI.
    /// Ключ — имя команды, которое используется CommandFactory для разрешения по строке.
    /// </summary>
    public static IServiceCollection AddBotCommands(this IServiceCollection services)
    {
        // Публичные команды (доступны из меню и по имени)
        services.AddKeyedScoped<IBotCommand, StartCommand>("start");
        services.AddKeyedScoped<IBotCommand, PreparationsListCommand>("preparations");
        services.AddKeyedScoped<IBotCommand, PharmaciesListCommand>("pharmacies");
        services.AddKeyedScoped<IBotCommand, FaqCommand>("faq");
        services.AddKeyedScoped<IBotCommand, InstructionCommand>("instruction");
        services.AddKeyedScoped<IBotCommand, ContactsCommand>("contacts");

        // Приватные (только через callback/сессию)
        services.AddKeyedScoped<IBotCommand, PreparationSearchCommand>("prepsearch");
        services.AddKeyedScoped<IBotCommand, PreparationDetailCommand>("prepinfo");
        services.AddKeyedScoped<IBotCommand, PharmacyMapCommand>("pharmmap");

        return services;
    }
}
