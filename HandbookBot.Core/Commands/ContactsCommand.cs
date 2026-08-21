using HandbookBot.Core.Interfaces;
using HandbookBot.Core.Models;

namespace HandbookBot.Core.Commands;

/// <summary>
/// Команда "contacts" — выводит контактную информацию ГАУЗ "ОБЛАСТНОЙ АПТЕЧНЫЙ СКЛАД".
/// </summary>
public sealed class ContactsCommand : IBotCommand
{
    public string Name => "contacts";

    public Task ExecuteAsync(BotContext context, IncomingMessage message, CancellationToken ct = default)
    {
        const string text =
            "*Контакты ГАУЗ ОАС*\n\n" +
            "Адрес: 460050 г. Оренбург, ул. Березка, 24\n\n" +
            "*Часы работы:*\n" +
            "пн–чт: 09:00–18:00\n" +
            "пт: 09:00–17:00\n" +
            "сб–вс: выходной\n\n" +
            "*Справочная:* +7 (3532) 507-507 доб. 200\n" +
            "пн–пт: 08:00–20:00\n" +
            "сб: 09:00–16:00\n" +
            "вс: выходной\n\n" +
            "E-mail: office@oas56.ru\n" +
            "Сайт: https://gosapteka.orb.ru/";

        return context.ReplyOrEditAsync(message, text,
            BotKeyboard.SingleColumn(BotButton.Callback("Главное меню", "start:menu")));
    }
}
