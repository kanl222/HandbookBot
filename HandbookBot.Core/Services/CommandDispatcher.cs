using HandbookBot.Core.Interfaces;
using HandbookBot.Core.Models;
using Microsoft.Extensions.Logging;

namespace HandbookBot.Core.Commands;

/// <summary>
/// Диспетчер команд: маршрутизирует входящие сообщения (<see cref="IncomingMessage"/>) 
/// к соответствующей команде бота (<see cref="IBotCommand"/>).
/// </summary>
public sealed class CommandDispatcher
{
    private readonly ICommandFactory _factory;
    private readonly IUserSessionStore _sessions;
    private readonly IRateLimiter _rateLimiter;
    private readonly ILogger<CommandDispatcher> _logger;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="CommandDispatcher"/>.
    /// </summary>
    /// <param name="factory">Фабрика для получения зарегистрированных команд.</param>
    /// <param name="sessions">Хранилище состояния сессий пользователей.</param>
    /// <param name="rateLimiter">Сервис ограничения частоты запросов.</param>
    /// <param name="logger">Экземпляр логгера.</param>
    public CommandDispatcher(
        ICommandFactory factory,
        IUserSessionStore sessions,
        IRateLimiter rateLimiter,
        ILogger<CommandDispatcher> logger)
    {
        _factory = factory;
        _sessions = sessions;
        _rateLimiter = rateLimiter;
        _logger = logger;
    }

    /// <summary>
    /// Обрабатывает входящее сообщение и вызовет подходящую команду на основе CallbackData, 
    /// активной сессии диалога или текста команды.
    /// </summary>
    /// <param name="message">Входящее сообщение от пользователя.</param>
    /// <param name="platform">Экземпляр платформы отправки ответов.</param>
    /// <param name="ct">Токен отмены.</param>
    public async Task DispatchAsync(IncomingMessage message, IMessagingPlatform platform, CancellationToken ct = default)
    {
        var context = new BotContext(
            ChatId: message.ChatId,
            UserId: message.UserId,
            Platform: message.Platform,
            MessagingPlatform: platform,
            Sessions: _sessions);

        // Проверка лимита частоты запросов для защиты от флуда и DoS
        if (!_rateLimiter.IsAllowed(context.SessionKey))
        {
            _logger.LogWarning("Превышен лимит запросов для {SessionKey}", context.SessionKey);
            await platform.SendTextAsync(message.ChatId, "Слишком много запросов. Пожалуйста, подождите несколько секунд.", ct: ct);
            return;
        }

        // Логирование метаданных на уровне Debug для защиты PII/чувствительных данных пользователя
        _logger.LogDebug(
            "Маршрутизация сообщения: Platform={Platform}, ChatId={ChatId}, UserId={UserId}",
            message.Platform, message.ChatId, message.UserId);

        // 1. Нажатие на Inline Callback-кнопку: формат "commandName:payload"
        if (!string.IsNullOrEmpty(message.CallbackData))
        {
            var commandKey = message.CallbackData.Split(':', 2)[0].Trim().ToLowerInvariant();
            var command = _factory.Resolve(commandKey);
            if (command is not null)
            {
                await command.ExecuteAsync(context, message, ct);
                return;
            }

            _logger.LogWarning("Неизвестный ключ callback-команды: '{Key}' (платформа: {Platform})", commandKey, message.Platform);
            await platform.SendTextAsync(message.ChatId, "Неизвестная кнопка. Нажмите /start для главного меню.", ct: ct);
            return;
        }

        // 2. Проверка наличия активной сессии диалога (например, ожидание текста поиска)
        var sessionKey = context.SessionKey;
        var sessionState = await _sessions.GetStateAsync(sessionKey, ct);
        if (sessionState is not null)
        {
            var sessionCommandKey = sessionState.AwaitingInputFor.Trim().ToLowerInvariant();
            var sessionCommand = _factory.Resolve(sessionCommandKey);
            if (sessionCommand is not null)
            {
                await sessionCommand.ExecuteAsync(context, message, ct);
                return;
            }

            _logger.LogWarning("Сессия ссылается на неизвестную команду '{Key}'. Очистка устаревшей сессии.", sessionCommandKey);
            await _sessions.ClearStateAsync(sessionKey, ct);
        }

        // 3. Вызов команды по текстовому имени (например, "/start" или "start")
        var text = message.Text?.Trim() ?? string.Empty;
        var commandName = text.StartsWith('/') ? text[1..] : text;
        var spaceIdx = commandName.IndexOf(' ');
        if (spaceIdx > 0) commandName = commandName[..spaceIdx];
        commandName = commandName.ToLowerInvariant();

        if (!string.IsNullOrEmpty(commandName))
        {
            var namedCommand = _factory.Resolve(commandName);
            if (namedCommand is not null)
            {
                await namedCommand.ExecuteAsync(context, message, ct);
                return;
            }
        }

        _logger.LogWarning("Обработчик для сообщения не найден (UserId: {UserId}, Platform: {Platform}): '{Text}'", message.UserId, message.Platform, message.Text);
        await platform.SendTextAsync(message.ChatId, "Команда не распознана. Воспользуйтесь /start для вызова главного меню.", ct: ct);
    }
}