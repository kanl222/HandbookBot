namespace HandbookBot.Core.Interfaces;

/// <summary>
/// Интерфейс для ограничения частоты входящих запросов (Rate Limiting).
/// </summary>
public interface IRateLimiter
{
    /// <summary>
    /// Проверяет, разрешён ли запрос для заданного ключа в текущий момент времени.
    /// </summary>
    /// <param name="key">Уникальный ключ клиента (например, SessionKey).</param>
    /// <returns>True, если лимит не превышен; иначе False.</returns>
    bool IsAllowed(string key);
}
