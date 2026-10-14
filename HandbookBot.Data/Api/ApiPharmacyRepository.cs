using System.Net.Http.Json;
using System.Collections.Concurrent;
using HandbookBot.Core.Interfaces;
using HandbookBot.Core.Models;

namespace HandbookBot.Data.Api;

/// <summary>
/// Репозиторий аптечных пунктов через RefInfoAPI.
/// Эндпоинты (JWT Bearer обязателен):
///   GET /api/drugstores?page={page}&pageSize={pageSize}
///   GET /api/drugstores/{id}
/// </summary>
public sealed class ApiPharmacyRepository : IPharmacyRepository
{
    private readonly HttpClient _http;

    public ApiPharmacyRepository(HttpClient http) => _http = http;

    public async Task<PagedResult<Pharmacy>> GetPageAsync(int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var response = await _http.GetFromJsonAsync<ApiPagedResult<ApiPharmacy>>(
            $"api/drugstores?page={page}&pageSize={pageSize}", ct)
            ?? new ApiPagedResult<ApiPharmacy>();

        return response.ToDomain(page, pageSize);
    }

    public async Task<Pharmacy?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        using var response = await _http.GetAsync($"api/drugstores/{id}", ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        var item = await response.Content.ReadFromJsonAsync<ApiPharmacy>(ct);
        return item?.ToDomain();
    }

    public async Task<IReadOnlyDictionary<int, Pharmacy>> GetByIdsAsync(IEnumerable<int> ids, CancellationToken ct = default)
    {
        var result = new ConcurrentDictionary<int, Pharmacy>();
        await Parallel.ForEachAsync(ids.Distinct(),
            new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = ct },
            async (id, token) =>
            {
                var pharmacy = await GetByIdAsync(id, token);
                if (pharmacy is not null)
                    result[id] = pharmacy;
            });
        return result;
    }
}
