using HandbookBot.Core.Interfaces;
using HandbookBot.Core.Models;

namespace HandbookBot.Tests.Helpers;

public class FakeMessagingPlatform : IMessagingPlatform
{
#pragma warning disable CS0067 // Событие не используется
    public event Func<IncomingMessage, Task>? OnMessageReceived;
#pragma warning restore CS0067

    public List<(string ChatId, string Text, BotKeyboard? Keyboard)> SentMessages { get; } = new();
    public List<(string ChatId, double Latitude, double Longitude)> SentLocations { get; } = new();

    public Task SendTextAsync(string chatId, string text, BotKeyboard? keyboard = null, CancellationToken ct = default)
    {
        SentMessages.Add((chatId, text, keyboard));
        return Task.CompletedTask;
    }

    public Task SendLocationAsync(string chatId, double latitude, double longitude, CancellationToken ct = default)
    {
        SentLocations.Add((chatId, latitude, longitude));
        return Task.CompletedTask;
    }
}
