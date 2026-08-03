namespace HandbookBot.Core.Models;

/// <summary>Состояние активного диалога пользователя (ожидание ввода).</summary>
public record UserDialogState(string AwaitingInputFor)
{
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public string? SearchQuery { get; init; }
}