namespace HandbookBot.Core.Models;

/// <summary>Входящее сообщение/событие от пользователя (платформо-независимое).</summary>
public sealed record IncomingMessage(
    string ChatId,
    string UserId,
    string Text,
    string? CallbackData,
    string Platform)
{
    public DateTime ReceivedAt { get; init; } = DateTime.UtcNow;
}
