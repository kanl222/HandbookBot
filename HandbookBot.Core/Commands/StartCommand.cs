using HandbookBot.Core.Interfaces;
using HandbookBot.Core.Models;

namespace HandbookBot.Core.Commands;

/// <summary>Команда /start — выводит главное меню. Также обрабатывает callback "start:menu".</summary>
public sealed class StartCommand : IBotCommand
{
    public string Name => "start";

    public async Task ExecuteAsync(BotContext context, IncomingMessage message, CancellationToken ct = default)
    {
        // Сбрасываем возможную сессию диалога
        await context.Sessions.ClearStateAsync(context.UserId, ct);

        var keyboard = BotKeyboard.SingleColumn(
            BotButton.Callback("Список препаратов", "preparations:1"),
            BotButton.Callback("Поиск препарата", "prepsearch:begin"),
            BotButton.Callback("Аптечные пункты", "pharmacies:1"),
            BotButton.Callback("Частые вопросы (FAQ)", "faq:list"),
            BotButton.Callback("Инструкция", "instruction:show"),
            BotButton.Callback("Контакты ГАУЗ ОАС", "contacts:show")
        );

        await context.ReplyAsync(
            "Добро пожаловать в справочник льготных препаратов ГАУЗ ОАС!\n\n" +
            "Выберите раздел:",
            keyboard);
    }
}
