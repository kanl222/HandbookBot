using HandbookBot.Core.Interfaces;

namespace HandbookBot.Core.Models;

public record BotContext(
    string ChatId,
    string UserId,
    string Platform,
    IMessagingPlatform MessagingPlatform,
    IUserSessionStore Sessions)
{
    public Task ReplyAsync(string text, BotKeyboard? keyboard = null)
        => MessagingPlatform.SendTextAsync(ChatId, text, keyboard);

    public Task SendLocationAsync(double latitude, double longitude)
        => MessagingPlatform.SendLocationAsync(ChatId, latitude, longitude);
}
