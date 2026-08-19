using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HandbookBot.Data.Api;

/// <summary>
/// Опции для подключения к RefInfoAPI.
/// Читается из секции конфига "RefInfoApi".
/// </summary>
public sealed class RefInfoApiOptions
{
    public const string Section = "RefInfoApi";

    /// <summary>Базовый URL API, например https://api.example.com</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Логин пользователя для получения JWT-токена.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>Пароль пользователя.</summary>
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// DelegatingHandler, который перед каждым запросом добавляет JWT Bearer-токен.
/// Токен кэшируется до истечения срока, после чего автоматически обновляется.
/// </summary>
public sealed class JwtAuthHandler : DelegatingHandler
{
    private readonly RefInfoApiOptions _options;
    private readonly ILogger<JwtAuthHandler> _logger;

    private string? _token;
    private DateTime _tokenExpiry = DateTime.MinValue;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public JwtAuthHandler(IOptions<RefInfoApiOptions> options, ILogger<JwtAuthHandler> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken ct)
    {
        var token = await GetTokenAsync(ct);
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await base.SendAsync(request, ct);

        // Если 401 — токен протух, сбрасываем и пробуем один раз повторно
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            _token = null;
            token = await GetTokenAsync(ct);
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            response = await base.SendAsync(request, ct);
        }

        return response;
    }

    private async Task<string> GetTokenAsync(CancellationToken ct)
    {
        if (_token is not null && DateTime.UtcNow < _tokenExpiry)
            return _token;

        await _lock.WaitAsync(ct);
        try
        {
            // Double-check после захвата блокировки
            if (_token is not null && DateTime.UtcNow < _tokenExpiry)
                return _token;

            _logger.LogInformation("RefInfoApi: получение нового JWT-токена...");

            using var client = new HttpClient { BaseAddress = new Uri(_options.BaseUrl.TrimEnd('/') + "/") };
            var response = await client.PostAsJsonAsync(
                "auth/login",
                new { username = _options.Username, password = _options.Password },
                ct);

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<LoginResponse>(ct)
                ?? throw new InvalidOperationException("RefInfoApi: пустой ответ на /auth/login");

            _token = result.AccessToken;
            // Обновляем за 1 минуту до истечения
            _tokenExpiry = result.ExpiresAt.AddMinutes(-1).ToUniversalTime();

            _logger.LogInformation("RefInfoApi: JWT получен, действителен до {Expiry}", _tokenExpiry);
            return _token;
        }
        finally
        {
            _lock.Release();
        }
    }

    private sealed class LoginResponse
    {
        [JsonPropertyName("accessToken")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("expiresAt")]
        public DateTime ExpiresAt { get; set; }
    }
}
