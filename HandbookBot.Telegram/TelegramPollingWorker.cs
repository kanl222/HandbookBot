using HandbookBot.Core.Commands;
using HandbookBot.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace HandbookBot.Telegram;

/// <summary>
/// Фоновый сервис (BackgroundService), отвечающий за получение обновлений от Telegram методом Long Polling.
/// Принимает новые сообщения и вызывает CommandDispatcher в отдельном DI Scope для каждого входящего запроса.
/// </summary>
public sealed class TelegramPollingWorker : BackgroundService, IDisposable
{
    private readonly ITelegramBotClient _client;
    private readonly TelegramPlatformAdapter _adapter;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TelegramPollingWorker> _logger;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="TelegramPollingWorker"/>.
    /// </summary>
    /// <param name="client">Клиент Telegram Bot API.</param>
    /// <param name="adapter">Адаптер платформы Telegram.</param>
    /// <param name="scopeFactory">Фабрика для создания Scoped-контейнеров DI при обработке сообщений.</param>
    /// <param name="logger">Логгер сервиса.</param>
    public TelegramPollingWorker(
        ITelegramBotClient client,
        TelegramPlatformAdapter adapter,
        IServiceScopeFactory scopeFactory,
        ILogger<TelegramPollingWorker> logger)
    {
        _client = client;
        _adapter = adapter;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Запускает цикл получения обновлений Telegram через Long Polling.
    /// </summary>
    /// <param name="stoppingToken">Токен отмены для остановки фоновой службы.</param>
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Запуск фоновой службы Telegram Polling Worker...");

        _client.StartReceiving(
            updateHandler: HandleUpdateAsync,
            errorHandler: HandleErrorAsync,
            cancellationToken: stoppingToken);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Обрабатывает входящее обновление Telegram в отдельном Scoped-контексте DI.
    /// </summary>
    private async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken ct)
    {
        try
        {
            // Маппим обновление Telegram в единую модель IncomingMessage
            var incoming = _adapter.MapUpdate(update);
            if (incoming is null)
                return;

            // Подтверждаем callback (убирает часики на кнопках в Telegram UI)
            await _adapter.AcknowledgeCallbackAsync(update);

            // Создаем отдельный Scoped DI контейнер для безопасности работы с DbContext и командами
            using var scope = _scopeFactory.CreateScope();
            var dispatcher = scope.ServiceProvider.GetRequiredService<CommandDispatcher>();

            // Передаем управление диспетчеру команд
            await dispatcher.DispatchAsync(incoming, _adapter, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при обработке обновления Telegram");
        }
    }

    /// <summary>
    /// Логирует ошибки получения обновлений от Telegram Bot API.
    /// </summary>
    private Task HandleErrorAsync(ITelegramBotClient bot, Exception ex, CancellationToken ct)
    {
        _logger.LogError(ex, "Ошибка соединения Telegram API в Polling Worker");
        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        base.Dispose();
    }
}