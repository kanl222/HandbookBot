using System.Net.Http.Json;
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
        var response = await _http.GetAsync($"api/drugstores/{id}", ct);
        if (!response.IsSuccessStatusCode)
            return null;

        var item = await response.Content.ReadFromJsonAsync<ApiPharmacy>(ct);
        return item?.ToDomain();
    }
}
