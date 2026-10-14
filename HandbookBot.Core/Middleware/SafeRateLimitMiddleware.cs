using BotEngine.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HandbookBot.Core.Middleware;

/// <summary>Ограничивает частоту сообщений без вложенных блокировок очередей пользователей.</summary>
public sealed class SafeRateLimitMiddleware : IBotMiddleware
{
    private sealed class Counter
    {
        public Queue<long> Timestamps { get; } = new();
        public int References { get; set; }
        public long LastSeen { get; set; }
    }

    private readonly Dictionary<string, Counter> _counters = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _sync = new();
    private readonly RateLimitOptions _options;
    private readonly ILogger<SafeRateLimitMiddleware> _logger;
    private readonly TimeProvider _timeProvider;
    private long _nextCleanup;

    public SafeRateLimitMiddleware(
        IOptions<RateLimitOptions> options,
        ILogger<SafeRateLimitMiddleware> logger,
        TimeProvider? timeProvider = null)
    {
        _options = options.Value;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task InvokeAsync(IncomingMessage message, IMessagingPlatform platform,
        BotMiddlewareDelegate next, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (IsThrottled($"{message.Platform}:{message.UserId}"))
        {
            _logger.LogWarning("Rate limit exceeded: UserId={UserId}, Platform={Platform}",
                message.UserId, message.Platform);
            if (_options.ThrottleMessage is not null)
                await platform.SendTextAsync(message.ChatId, _options.ThrottleMessage, ct: ct);
            return;
        }

        await next(message, platform, ct);
    }

    private bool IsThrottled(string key)
    {
        var windowMs = _options.WindowSeconds * 1000L;
        Counter counter;

        lock (_sync)
        {
            var now = _timeProvider.GetUtcNow().ToUnixTimeMilliseconds();
            var cutoff = now - windowMs;
            // Очередь не может изменяться при References == 0. Новые обращения
            // увеличивают References под тем же _sync, поэтому удаление безопасно.
            if (_counters.Count > 10_000 && now >= _nextCleanup)
            {
                _nextCleanup = now + 60_000;
                var expired = _counters
                    .Where(pair => pair.Value.References == 0 && pair.Value.LastSeen < cutoff)
                    .Select(pair => pair.Key)
                    .ToArray();
                foreach (var expiredKey in expired)
                    _counters.Remove(expiredKey);
            }

            if (!_counters.TryGetValue(key, out counter!))
            {
                counter = new Counter();
                _counters.Add(key, counter);
            }
            counter.References++;
            counter.LastSeen = now;
        }

        try
        {
            lock (counter.Timestamps)
            {
                var now = _timeProvider.GetUtcNow().ToUnixTimeMilliseconds();
                var cutoff = now - windowMs;
                while (counter.Timestamps.Count > 0 && counter.Timestamps.Peek() < cutoff)
                    counter.Timestamps.Dequeue();

                if (counter.Timestamps.Count >= _options.MaxMessages)
                    return true;

                counter.Timestamps.Enqueue(now);
                return false;
            }
        }
        finally
        {
            // Блокировка очереди уже освобождена: вложенных lock нет.
            lock (_sync)
            {
                counter.References--;
                counter.LastSeen = _timeProvider.GetUtcNow().ToUnixTimeMilliseconds();
            }
        }
    }
}
