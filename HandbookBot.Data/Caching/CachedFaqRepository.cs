using System.Text.Json;
using HandbookBot.Core.Interfaces;
using HandbookBot.Core.Models;
using Microsoft.Extensions.Caching.Distributed;

namespace HandbookBot.Data.Caching;

/// <summary>
/// Декоратор кэширования для IFaqRepository с использованием распределенного кэша IDistributedCache.
/// </summary>
public sealed class CachedFaqRepository : IFaqRepository
{
    private readonly IFaqRepository _inner;
    private readonly IDistributedCache _cache;
    private const string CacheKey = "FaqList";
    private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(30);
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public CachedFaqRepository(IFaqRepository inner, IDistributedCache cache)
    {
        _inner = inner;
        _cache = cache;
    }

    public async Task<IReadOnlyList<FaqEntry>> GetAllAsync(CancellationToken ct = default)
    {
        var cachedData = await _cache.GetStringAsync(CacheKey, ct);
        if (!string.IsNullOrEmpty(cachedData))
        {
            try
            {
                var cachedFaq = JsonSerializer.Deserialize<List<FaqEntry>>(cachedData, _jsonOptions);
                if (cachedFaq is not null)
                {
                    return cachedFaq;
                }
            }
            catch
            {
                // Игнорируем ошибки десериализации, идем в нижележащий репозиторий
            }
        }

        var faq = await _inner.GetAllAsync(ct);
        
        try
        {
            var serialized = JsonSerializer.Serialize(faq, _jsonOptions);
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = _cacheDuration
            };
            await _cache.SetStringAsync(CacheKey, serialized, options, ct);
        }
        catch
        {
            // Игнорируем ошибки сериализации
        }

        return faq;
    }
}
