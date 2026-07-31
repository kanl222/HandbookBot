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
            "*Контакты*\n\n" +
            "460050 г. Оренбург ул. Березка, 24\n\n" +
            "пн - чт: 09:00 - 18:00, пт: 09:00 - 17:00, сб - вс: выходной\n\n" +
            "Справочная: +7 (3532) 507-507 доб. 200\n\n" +
            "пн - пт: 08:00 - 20:00, сб: 09:00 - 16:00, вс: выходной\n\n" +
            "e-mail: office@oas56.ru\n\n"+
            "сайт: https://gosapteka.orb.ru/";

        return context.ReplyAsync(text,
            BotKeyboard.SingleColumn(BotButton.Callback("Главное меню", "start:menu")));
    }
}
