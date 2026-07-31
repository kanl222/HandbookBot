using HandbookBot.Core.Models;
using MAX.Bot.Interfaces.Models.Request.Message.Attachment;
using MAX.Bot.Interfaces.Models.Request.Message.Attachment.Payloads;
namespace HandbookBot.Max.Mapping;

internal static class ButtonMapper
{
    public static InlineKeyboardAttachment ToInlineKeyboardAttachment(BotKeyboard keyboard)
    {
        var rows = keyboard.Rows
            .Select(row => row.Select(ToMaxButton).ToList())
            .ToList();

        return new InlineKeyboardAttachment
        {
            Payload = new InlineKeyboardPayload { Buttons = rows }
        };
    }

    private static Button ToMaxButton(BotButton button) => button.Type switch
    {
        BotButtonType.Callback => new CallbackButton { Text = button.Text, Payload = button.Payload ?? button.Text },
        BotButtonType.Url => new LinkButton { Text = button.Text, Url = button.Url ?? string.Empty },
        BotButtonType.RequestGeo => new RequestGeoButton { Text = button.Text, Quick = true },
        BotButtonType.RequestText => new MessageButton { Text = button.Text },
        _ => throw new ArgumentOutOfRangeException(nameof(button.Type), $"Unsupported button type: {button.Type}")
    };
}