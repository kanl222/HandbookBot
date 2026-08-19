using System.Net.Http.Json;
using HandbookBot.Core.Interfaces;
using HandbookBot.Core.Models;

namespace HandbookBot.Data.Api;

/// <summary>
/// Репозиторий препаратов через RefInfoAPI.
/// Эндпоинты (JWT Bearer обязателен):
///   GET /api/drugs?page={page}&pageSize={pageSize}
///   GET /api/drugs/search?query={q}&page={page}&pageSize={pageSize}
///   GET /api/drugs/{id}
/// </summary>
public sealed class ApiPreparationRepository : IPreparationRepository
{
    private readonly HttpClient _http;

    public ApiPreparationRepository(HttpClient http) => _http = http;

    public async Task<PagedResult<Preparation>> GetPageAsync(int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var response = await _http.GetFromJsonAsync<ApiPagedResult<ApiPreparation>>(
            $"api/drugs?page={page}&pageSize={pageSize}", ct)
            ?? new ApiPagedResult<ApiPreparation>();

        return response.ToDomain(page, pageSize);
    }

    public async Task<PagedResult<Preparation>> SearchAsync(string query, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var encodedQuery = Uri.EscapeDataString(query.Trim());

        var response = await _http.GetFromJsonAsync<ApiPagedResult<ApiPreparation>>(
            $"api/drugs/search?query={encodedQuery}&page={page}&pageSize={pageSize}", ct)
            ?? new ApiPagedResult<ApiPreparation>();

        return response.ToDomain(page, pageSize);
    }

    public async Task<Preparation?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"api/drugs/{id}", ct);
        if (!response.IsSuccessStatusCode)
            return null;

        var item = await response.Content.ReadFromJsonAsync<ApiPreparation>(ct);
        return item?.ToDomain();
    }
}
