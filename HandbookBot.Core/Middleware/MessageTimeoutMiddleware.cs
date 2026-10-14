using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HandbookBot.Core.Middleware;

public sealed class MessageProcessingOptions
{
    public const string SectionName = "HandbookBot";
    public int MessageTimeoutSeconds { get; set; } = 60;
}

/// <summary>Ограничивает время ожидания очереди и выполнения команды кооперативной отменой.</summary>
public sealed class MessageTimeoutMiddleware : IBotMiddleware
{
    private readonly TimeSpan _timeout;
    private readonly ILogger<MessageTimeoutMiddleware> _logger;

    public MessageTimeoutMiddleware(IOptions<MessageProcessingOptions> options,
        ILogger<MessageTimeoutMiddleware> logger)
    {
        _timeout = TimeSpan.FromSeconds(options.Value.MessageTimeoutSeconds);
        _logger = logger;
    }

    public async Task InvokeAsync(IncomingMessage message, IMessagingPlatform platform,
        BotMiddlewareDelegate next, CancellationToken ct = default)
    {
        using var deadline = new CancellationTokenSource(_timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, deadline.Token);
        try
        {
            // Дожидаемся реального завершения операции: пользовательский семафор
            // нельзя освободить, пока прежняя команда ещё меняет сессию или отправляет ответ.
            await next(message, platform, linked.Token);
            linked.Token.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            _logger.LogWarning("Превышено время обработки сообщения: Platform={Platform}, UserId={UserId}, TimeoutSeconds={TimeoutSeconds}",
                message.Platform, message.UserId, _timeout.TotalSeconds);
            using var notification = CancellationTokenSource.CreateLinkedTokenSource(ct);
            notification.CancelAfter(TimeSpan.FromSeconds(5));
            try
            {
                await platform.SendTextAsync(message.ChatId,
                    "Обработка запроса заняла слишком много времени. Попробуйте ещё раз.",
                    ct: notification.Token);
            }
            catch (OperationCanceledException) when (notification.IsCancellationRequested)
            {
                ct.ThrowIfCancellationRequested();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Не удалось отправить уведомление о таймауте");
            }
        }
    }
}
