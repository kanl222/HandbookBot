using System.Collections.Concurrent;
using HandbookBot.Core.Interfaces;

namespace HandbookBot.Core.Services;

/// <summary>
/// Потокобезопасный ограничитель частоты запросов на основе алгоритма скользящего окна (Sliding Window).
/// Защищает приложение от флуда и DoS-атак со стороны отдельных пользователей.
/// </summary>
public sealed class SlidingWindowRateLimiter : IRateLimiter
{
    private readonly int _maxRequests;
    private readonly TimeSpan _window;
    private readonly ConcurrentDictionary<string, Queue<long>> _records = new();
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Создает экземпляр ограничителя частоты запросов.
    /// </summary>
    /// <param name="maxRequests">Максимальное количество запросов за окно (по умолчанию 5).</param>
    /// <param name="window">Размер временного окна (по умолчанию 3 секунды).</param>
    /// <param name="timeProvider">Провайдер времени для возможности тестирования.</param>
    public SlidingWindowRateLimiter(int maxRequests = 5, TimeSpan? window = null, TimeProvider? timeProvider = null)
    {
        _maxRequests = Math.Max(1, maxRequests);
        _window = window ?? TimeSpan.FromSeconds(3);
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc/>
    public bool IsAllowed(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return true;

        var nowTicks = _timeProvider.GetUtcNow().UtcTicks;
        var windowTicks = _window.Ticks;

        var queue = _records.GetOrAdd(key.ToLowerInvariant(), _ => new Queue<long>());
        lock (queue)
        {
            while (queue.Count > 0 && nowTicks - queue.Peek() > windowTicks)
            {
                queue.Dequeue();
            }

            if (queue.Count >= _maxRequests)
            {
                return false;
            }

            queue.Enqueue(nowTicks);
            return true;
        }
    }
}
