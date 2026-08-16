using HandbookBot.Core.Interfaces;

namespace HandbookBot.Core.Models;

public record BotContext(
    string ChatId,
    string UserId,
    string Platform,
    IMessagingPlatform MessagingPlatform,
    IUserSessionStore Sessions)
{
    /// <summary>
    /// Уникальный ключ сессии с изоляцией по платформе (исключает межплатформенные коллизии).
    /// </summary>
    public string SessionKey => $"{Platform}:{UserId}".ToLowerInvariant();

    public Task ReplyAsync(string text, BotKeyboard? keyboard = null)
        => MessagingPlatform.SendTextAsync(ChatId, text, keyboard);

    public Task SendLocationAsync(double latitude, double longitude)
        => MessagingPlatform.SendLocationAsync(ChatId, latitude, longitude);
}
