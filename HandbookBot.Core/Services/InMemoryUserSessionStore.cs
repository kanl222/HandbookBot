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

    public Task<UserDialogState?> GetStateAsync(string userId, CancellationToken ct = default)
    {
        if (_store.TryGetValue(userId, out var entry))
        {
            if (entry.ExpiresAt > DateTimeOffset.UtcNow)
                return Task.FromResult<UserDialogState?>(entry.State);

            _store.TryRemove(userId, out _);
        }
        return Task.FromResult<UserDialogState?>(null);
    }

    public Task SetStateAsync(string userId, UserDialogState state, TimeSpan? ttl = null, CancellationToken ct = default)
    {
        var expires = DateTimeOffset.UtcNow.Add(ttl ?? TimeSpan.FromMinutes(10));
        _store[userId] = new Entry(state, expires);
        return Task.CompletedTask;
    }

    public Task ClearStateAsync(string userId, CancellationToken ct = default)
    {
        _store.TryRemove(userId, out _);
        return Task.CompletedTask;
    }
}
