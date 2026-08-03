using System.Text.Json;
using HandbookBot.Core.Interfaces;
using HandbookBot.Core.Models;
using Microsoft.Extensions.Caching.Distributed;

namespace HandbookBot.Data.Caching;

/// <summary>
/// Декоратор кэширования для IPharmacyRepository с использованием распределенного кэша IDistributedCache.
/// </summary>
public sealed class CachedPharmacyRepository : IPharmacyRepository
{
    private readonly IPharmacyRepository _inner;
    private readonly IDistributedCache _cache;
    private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(30);
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public CachedPharmacyRepository(IPharmacyRepository inner, IDistributedCache cache)
    {
        _inner = inner;
        _cache = cache;
    }

    public async Task<PagedResult<Pharmacy>> GetPageAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var cacheKey = $"PharmacyList_Page_{page}_{pageSize}";
        var cachedData = await _cache.GetStringAsync(cacheKey, ct);
        if (!string.IsNullOrEmpty(cachedData))
        {
            try
            {
                var cachedList = JsonSerializer.Deserialize<PagedResult<Pharmacy>>(cachedData, _jsonOptions);
                if (cachedList is not null)
                {
                    return cachedList;
                }
            }
            catch
            {
                // Игнорируем ошибки десериализации, идем в бд
            }
        }

        var list = await _inner.GetPageAsync(page, pageSize, ct);
        
        try
        {
            var serialized = JsonSerializer.Serialize(list, _jsonOptions);
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = _cacheDuration
            };
            await _cache.SetStringAsync(cacheKey, serialized, options, ct);
        }
        catch
        {
            // Игнорируем ошибки сериализации
        }

        return list;
    }

    public async Task<Pharmacy?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var cacheKey = $"Pharmacy_{id}";
        var cachedData = await _cache.GetStringAsync(cacheKey, ct);
        if (!string.IsNullOrEmpty(cachedData))
        {
            try
            {
                var cachedItem = JsonSerializer.Deserialize<Pharmacy>(cachedData, _jsonOptions);
                if (cachedItem is not null)
                {
                    return cachedItem;
                }
            }
            catch
            {
                // Игнорируем ошибки десериализации, идем в бд
            }
        }

        var item = await _inner.GetByIdAsync(id, ct);
        
        if (item is not null)
        {
            try
            {
                var serialized = JsonSerializer.Serialize(item, _jsonOptions);
                var options = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = _cacheDuration
                };
                await _cache.SetStringAsync(cacheKey, serialized, options, ct);
            }
            catch
            {
                // Игнорируем ошибки сериализации
            }
        }

        return item;
    }
}
