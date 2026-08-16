using System.Text.Json;
using HandbookBot.Core.Interfaces;
using HandbookBot.Core.Models;
using Microsoft.Extensions.Caching.Distributed;

namespace HandbookBot.Data.Caching;

/// <summary>
/// Реализация хранилища сессий пользователей с использованием распределенного кэша IDistributedCache.
/// </summary>
public sealed class DistributedCacheUserSessionStore : IUserSessionStore
{
    private readonly IDistributedCache _cache;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public DistributedCacheUserSessionStore(IDistributedCache cache)
    {
        _cache = cache;
    }

    private static string GetKey(string sessionKey) => $"session:{sessionKey.ToLowerInvariant()}";

    public async Task<UserDialogState?> GetStateAsync(string sessionKey, CancellationToken ct = default)
    {
        var data = await _cache.GetStringAsync(GetKey(sessionKey), ct);
        if (string.IsNullOrEmpty(data))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<UserDialogState>(data, _jsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public async Task SetStateAsync(string sessionKey, UserDialogState state, TimeSpan? ttl = null, CancellationToken ct = default)
    {
        var data = JsonSerializer.Serialize(state, _jsonOptions);
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ttl ?? TimeSpan.FromMinutes(10)
        };

        await _cache.SetStringAsync(GetKey(sessionKey), data, options, ct);
    }

    public async Task ClearStateAsync(string sessionKey, CancellationToken ct = default)
    {
        await _cache.RemoveAsync(GetKey(sessionKey), ct);
    }
}
