using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.ExceptionServices;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HandbookBot.Data.Api;

/// <summary>Настройки подключения к RefInfoAPI.</summary>
public sealed class RefInfoApiOptions
{
    public const string Section = "RefInfoApi";
    public string BaseUrl { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

/// <summary>Общий кэш JWT с единственным обновлением и паузой после ошибок login.</summary>
public sealed class JwtTokenProvider
{
    public const string HttpClientName = "RefInfoApiAuth";
    private static readonly TimeSpan FailureCooldown = TimeSpan.FromSeconds(5);
    private sealed record TokenState(string Value, DateTimeOffset ExpiresAt);

    private readonly RefInfoApiOptions _options;
    private readonly ILogger<JwtTokenProvider> _logger;
    private readonly IHttpClientFactory _clients;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _refresh = new(1, 1);
    private readonly object _sync = new();
    private TokenState? _token;
    private ExceptionDispatchInfo? _failure;
    private DateTimeOffset _retryAfter;
    private long _generation;

    public JwtTokenProvider(IOptions<RefInfoApiOptions> options, ILogger<JwtTokenProvider> logger,
        IHttpClientFactory clients, TimeProvider? timeProvider = null)
    {
        _options = options.Value;
        _logger = logger;
        _clients = clients;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public void InvalidateToken(string? rejectedToken = null)
    {
        lock (_sync)
        {
            // Запоздалый 401 не должен сбрасывать токен, уже заменённый другим запросом.
            if (rejectedToken is not null && _token?.Value != rejectedToken)
                return;
            _token = null;
            _generation++;
        }
    }

    public async Task<string?> GetTokenAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(_options.Username) ||
            string.IsNullOrWhiteSpace(_options.Password) ||
            string.IsNullOrWhiteSpace(_options.BaseUrl))
            return null;

        if (TryGetCachedToken(out var cached))
            return cached;

        await _refresh.WaitAsync(ct);
        try
        {
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                if (TryGetCachedToken(out cached))
                    return cached;

                long generation;
                lock (_sync)
                    generation = _generation;

                try
                {
                    _logger.LogInformation("RefInfoApi: получение нового JWT-токена...");
                    using var client = _clients.CreateClient(HttpClientName);
                    client.BaseAddress = new Uri(_options.BaseUrl.TrimEnd('/') + "/");
                    client.Timeout = TimeSpan.FromSeconds(15);
                    using var response = await client.PostAsJsonAsync("auth/login",
                        new { username = _options.Username, password = _options.Password }, ct);
                    response.EnsureSuccessStatusCode();
                    var result = await response.Content.ReadFromJsonAsync<LoginResponse>(ct)
                        ?? throw new InvalidOperationException("RefInfoApi: пустой ответ на /auth/login");
                    if (string.IsNullOrWhiteSpace(result.AccessToken))
                        throw new InvalidOperationException("RefInfoApi: ответ login не содержит accessToken");

                    var now = _timeProvider.GetUtcNow();
                    var expires = new DateTimeOffset(result.ExpiresAt.ToUniversalTime());
                    var expiry = expires > now
                        ? expires - TimeSpan.FromSeconds(Math.Min(60, (expires - now).TotalSeconds / 2))
                        : now.AddMinutes(30);

                    lock (_sync)
                    {
                        // Безусловное инвалидирование во время login требует новой попытки.
                        if (generation != _generation)
                            continue;
                        _token = new TokenState(result.AccessToken, expiry);
                        _failure = null;
                    }
                    _logger.LogInformation("RefInfoApi: JWT получен, действителен до {Expiry}", expiry);
                    return result.AccessToken;
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    // Отмена одного сообщения не должна включать паузу для остальных.
                    throw;
                }
                catch (Exception ex)
                {
                    lock (_sync)
                    {
                        _failure = ExceptionDispatchInfo.Capture(ex);
                        _retryAfter = _timeProvider.GetUtcNow() + FailureCooldown;
                    }
                    _logger.LogError(ex, "RefInfoApi: ошибка при получении JWT-токена");
                    throw;
                }
            }
        }
        finally
        {
            _refresh.Release();
        }
    }

    private bool TryGetCachedToken(out string? token)
    {
        lock (_sync)
        {
            var now = _timeProvider.GetUtcNow();
            if (_token is not null && now < _token.ExpiresAt)
            {
                token = _token.Value;
                return true;
            }
            if (_failure is not null && now < _retryAfter)
                _failure.Throw();
            token = null;
            return false;
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

/// <summary>Добавляет Bearer-токен и один раз повторяет запрос после 401.</summary>
public sealed class JwtAuthHandler : DelegatingHandler
{
    private readonly JwtTokenProvider _tokenProvider;

    public JwtAuthHandler(JwtTokenProvider tokenProvider) => _tokenProvider = tokenProvider;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var token = await _tokenProvider.GetTokenAsync(ct);
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await base.SendAsync(request, ct);
        if (response.StatusCode != System.Net.HttpStatusCode.Unauthorized || string.IsNullOrEmpty(token))
            return response;

        try
        {
            _tokenProvider.InvalidateToken(token);
            var refreshed = await _tokenProvider.GetTokenAsync(ct);
            if (string.IsNullOrEmpty(refreshed))
                return response;

            using var retry = await CloneRequestAsync(request, ct);
            retry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", refreshed);
            response.Dispose();
            return await base.SendAsync(retry, ct);
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }

    private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version,
            VersionPolicy = request.VersionPolicy
        };
        foreach (var header in request.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        foreach (var option in request.Options)
            clone.Options.Set(new HttpRequestOptionsKey<object?>(option.Key), option.Value);
        if (request.Content is not null)
        {
            clone.Content = new ByteArrayContent(await request.Content.ReadAsByteArrayAsync(ct));
            foreach (var header in request.Content.Headers)
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }
        return clone;
    }
}

