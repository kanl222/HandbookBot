using HandbookBot.Core.Interfaces;
using HandbookBot.Core.Models;
using Microsoft.Extensions.Caching.Memory;

namespace HandbookBot.Data.Caching;

public sealed class CachedFaqRepository : IFaqRepository
{
    private readonly IFaqRepository _inner;
    private readonly IMemoryCache _cache;
    private const string CacheKey = "FaqList";
    private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(30);

    public CachedFaqRepository(IFaqRepository inner, IMemoryCache cache)
    {
        _inner = inner;
        _cache = cache;
    }

    public async Task<IReadOnlyList<FaqEntry>> GetAllAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue(CacheKey, out IReadOnlyList<FaqEntry>? cachedFaq) && cachedFaq is not null)
        {
            return cachedFaq;
        }

        var faq = await _inner.GetAllAsync(ct);
        _cache.Set(CacheKey, faq, _cacheDuration);

        return faq;
    }
}
