using System.Net.Http.Json;
using HandbookBot.Core.Interfaces;
using HandbookBot.Core.Models;

namespace HandbookBot.Data.Api;

/// <summary>
/// API-заглушка репозитория FAQ.
/// Ожидаемый эндпоинт API:
///   GET /api/faq  → IEnumerable&lt;ApiFaqEntry&gt;
/// </summary>
public sealed class ApiFaqRepository : IFaqRepository
{
    private readonly HttpClient _http;

    public ApiFaqRepository(HttpClient http) => _http = http;

    public async Task<IReadOnlyList<FaqEntry>> GetAllAsync(CancellationToken ct = default)
    {
        var items = await _http.GetFromJsonAsync<List<ApiFaqEntry>>("api/faq", ct)
            ?? [];

        return items
            .Select(x => x.ToDomain())
            .ToList()
            .AsReadOnly();
    }
}
