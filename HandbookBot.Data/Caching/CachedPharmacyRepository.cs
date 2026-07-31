using HandbookBot.Core.Interfaces;
using HandbookBot.Core.Models;
using Microsoft.Extensions.Caching.Memory;

namespace HandbookBot.Data.Caching;

public sealed class CachedPharmacyRepository : IPharmacyRepository
{
    private readonly IPharmacyRepository _inner;
    private readonly IMemoryCache _cache;
    private const string CacheKeyAll = "PharmacyList_All";
    private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(30);

    public CachedPharmacyRepository(IPharmacyRepository inner, IMemoryCache cache)
    {
        _inner = inner;
        _cache = cache;
    }

    public async Task<PagedResult<Pharmacy>> GetPageAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var cacheKey = $"PharmacyList_Page_{page}_{pageSize}";
        
        if (_cache.TryGetValue(cacheKey, out PagedResult<Pharmacy>? cachedList) && cachedList is not null)
        {
            return cachedList;
        }

        var list = await _inner.GetPageAsync(page, pageSize, ct);
        _cache.Set(cacheKey, list, _cacheDuration);

        return list;
    }

    public async Task<Pharmacy?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var cacheKey = $"Pharmacy_{id}";
        
        if (_cache.TryGetValue(cacheKey, out Pharmacy? cachedItem))
        {
            return cachedItem;
        }

        var item = await _inner.GetByIdAsync(id, ct);
        
        if (item is not null)
        {
            _cache.Set(cacheKey, item, _cacheDuration);
        }

        return item;
    }
}
