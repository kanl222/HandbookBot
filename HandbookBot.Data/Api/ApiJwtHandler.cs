using System.Net.Http.Headers;
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
/// Провайдер JWT-токенов (Singleton).
/// Хранит кэшированный токен и обновляет его при истечении.
/// </summary>
public sealed class JwtTokenProvider
{
    private readonly RefInfoApiOptions _options;
    private readonly ILogger<JwtTokenProvider> _logger;

    private string? _token;
    private DateTime _tokenExpiry = DateTime.MinValue;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public JwtTokenProvider(IOptions<RefInfoApiOptions> options, ILogger<JwtTokenProvider> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public void InvalidateToken()
    {
        _token = null;
        _tokenExpiry = DateTime.MinValue;
    }

    public async Task<string?> GetTokenAsync(CancellationToken ct = default)
    {
        // Если логин или пароль не заданы — пропускаем авторизацию (например, локальный mock API)
        if (string.IsNullOrWhiteSpace(_options.Username) || string.IsNullOrWhiteSpace(_options.Password))
            return null;

        if (_token is not null && DateTime.UtcNow < _tokenExpiry)
            return _token;

        await _lock.WaitAsync(ct);
        try
        {
            if (_token is not null && DateTime.UtcNow < _tokenExpiry)
                return _token;

            if (string.IsNullOrWhiteSpace(_options.BaseUrl))
                return null;

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
            _tokenExpiry = result.ExpiresAt > DateTime.UtcNow 
                ? result.ExpiresAt.AddMinutes(-1).ToUniversalTime() 
                : DateTime.UtcNow.AddMinutes(30);

            _logger.LogInformation("RefInfoApi: JWT получен, действителен до {Expiry}", _tokenExpiry);
            return _token;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RefInfoApi: ошибка при получении JWT-токена");
            throw;
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

/// <summary>
/// DelegatingHandler (Transient), который перед каждым запросом добавляет JWT Bearer-токен.
/// </summary>
public sealed class JwtAuthHandler : DelegatingHandler
{
    private readonly JwtTokenProvider _tokenProvider;

    public JwtAuthHandler(JwtTokenProvider tokenProvider)
    {
        _tokenProvider = tokenProvider;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken ct)
    {
        var token = await _tokenProvider.GetTokenAsync(ct);
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        var response = await base.SendAsync(request, ct);

        // Если 401 — токен протух, сбрасываем и пробуем один раз повторно
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized && !string.IsNullOrEmpty(token))
        {
            _tokenProvider.InvalidateToken();
            var refreshedToken = await _tokenProvider.GetTokenAsync(ct);
            if (!string.IsNullOrEmpty(refreshedToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", refreshedToken);
                response = await base.SendAsync(request, ct);
            }
        }

        return response;
    }
}

