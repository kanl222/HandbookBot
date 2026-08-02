using HandbookBot.Core.Models;

namespace HandbookBot.Core.Interfaces;

public interface IMessagingPlatform
{
    string Name { get; init; }
    Task SendTextAsync(string chatId, string text, BotKeyboard? keyboard = null, CancellationToken ct = default);
    Task SendLocationAsync(string chatId, double latitude, double longitude, CancellationToken ct = default);
    event Func<IncomingMessage, Task>? OnMessageReceived;
}
