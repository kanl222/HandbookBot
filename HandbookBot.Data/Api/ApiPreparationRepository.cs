using System.Net.Http.Json;
using HandbookBot.Core.Interfaces;
using HandbookBot.Core.Models;
using Microsoft.Extensions.Logging;

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
    private readonly ILogger<ApiPreparationRepository> _logger;

    public ApiPreparationRepository(HttpClient http, ILogger<ApiPreparationRepository> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<PagedResult<Preparation>> GetPageAsync(int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var url = $"api/drugs?page={page}&pageSize={pageSize}";

        try
        {
            var response = await _http.GetFromJsonAsync<ApiPagedResult<ApiPreparation>>(url, ct)
                ?? new ApiPagedResult<ApiPreparation>();

            return response.ToDomain(page, pageSize);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при запросе к API: GET {BaseAddress}{Url}", _http.BaseAddress, url);
            throw;
        }
    }

    public async Task<PagedResult<Preparation>> SearchAsync(string query, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var encodedQuery = Uri.EscapeDataString(query.Trim());
        var url = $"api/drugs/search?query={encodedQuery}&page={page}&pageSize={pageSize}";

        try
        {
            var response = await _http.GetFromJsonAsync<ApiPagedResult<ApiPreparation>>(url, ct)
                ?? new ApiPagedResult<ApiPreparation>();

            return response.ToDomain(page, pageSize);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при поиске через API: GET {BaseAddress}{Url}", _http.BaseAddress, url);
            throw;
        }
    }

    public async Task<Preparation?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var url = $"api/drugs/{id}";
        try
        {
            var response = await _http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("API вернул статус {StatusCode} на запрос {BaseAddress}{Url}", response.StatusCode, _http.BaseAddress, url);
                return null;
            }

            var item = await response.Content.ReadFromJsonAsync<ApiPreparation>(ct);
            return item?.ToDomain();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении препарата по Id={Id} через API: GET {BaseAddress}{Url}", id, _http.BaseAddress, url);
            throw;
        }
    }
}

