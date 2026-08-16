using System.Collections.Concurrent;
using HandbookBot.Core.Interfaces;
using HandbookBot.Core.Models;

namespace HandbookBot.Core.Services;

/// <summary>
/// Хранилище состояний диалогов в оперативной памяти.
/// Подходит для одиночного экземпляра приложения без горизонтального масштабирования.
/// </summary>
public sealed class InMemoryUserSessionStore : IUserSessionStore
{
    private sealed record Entry(UserDialogState State, DateTimeOffset ExpiresAt);

    private readonly ConcurrentDictionary<string, Entry> _store = new();

    public Task<UserDialogState?> GetStateAsync(string sessionKey, CancellationToken ct = default)
    {
        var key = sessionKey.ToLowerInvariant();
        if (_store.TryGetValue(key, out var entry))
        {
            if (entry.ExpiresAt > DateTimeOffset.UtcNow)
                return Task.FromResult<UserDialogState?>(entry.State);

            _store.TryRemove(key, out _);
        }
        return Task.FromResult<UserDialogState?>(null);
    }

    public Task SetStateAsync(string sessionKey, UserDialogState state, TimeSpan? ttl = null, CancellationToken ct = default)
    {
        var key = sessionKey.ToLowerInvariant();
        var expires = DateTimeOffset.UtcNow.Add(ttl ?? TimeSpan.FromMinutes(10));
        _store[key] = new Entry(state, expires);
        return Task.CompletedTask;
    }

    public Task ClearStateAsync(string sessionKey, CancellationToken ct = default)
    {
        _store.TryRemove(sessionKey.ToLowerInvariant(), out _);
        return Task.CompletedTask;
    }
}
