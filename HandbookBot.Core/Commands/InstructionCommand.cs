using HandbookBot.Core.Interfaces;
using HandbookBot.Core.Models;

namespace HandbookBot.Core.Commands;

/// <summary>
/// Команда "instruction" — выводит инструкцию по использованию чат-бота и получению услуг.
/// </summary>
public sealed class InstructionCommand : IBotCommand
{
    public string Name => "instruction";

    public Task ExecuteAsync(BotContext context, IncomingMessage message, CancellationToken ct = default)
    {
        const string text =
            "*Инструкция по использованию чат-бота*\n\n" +
            "Данный справочник помогает льготным категориям граждан быстро находить лекарства и аптечные пункты ГАУЗ ОАС.\n\n" +
            "*Основные функции бота:*\n" +
            "• *Список препаратов* — просмотр каталога доступных лекарств с указанием цен и наличия.\n" +
            "• *Поиск препарата* — быстрый поиск медикаментов по фрагменту названия.\n" +
            "• *Аптечные пункты* — список адресов и контактов аптек с возможностью просмотра на карте.\n" +
            "• *Частые вопросы (FAQ)* — справочная информация и ответы на вопросы.\n\n" +
            "*Как пользоваться ботом:*\n" +
            "1. Нажмите /start для вызова главного меню.\n" +
            "2. Выберите нужный раздел с помощью интерактивных кнопок.\n" +
            "3. Для поиска медикамента нажмите *«Поиск препарата»* и введите его название ответным сообщением.\n" +
            "4. Для просмотра расположения аптеки нажмите кнопку *«Карта»* рядом с её описанием.\n\n";

        return context.ReplyAsync(text,
            BotKeyboard.SingleColumn(BotButton.Callback("Главное меню", "start:menu")));
    }
}
