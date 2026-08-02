using HandbookBot.Core.Interfaces;
using HandbookBot.Core.Models;
using HandbookBot.Max.Mapping;
using MAX.Bot.Interfaces;
using MAX.Bot.Interfaces.Models;
using MAX.Bot.Interfaces.Models.Request.Message;
using MAX.Bot.Interfaces.Models.Request.Message.Attachment;
using Microsoft.Extensions.Logging;

namespace HandbookBot.Max;

/// <summary>
/// Адаптер MAX-платформы: реализует IMessagingPlatform через IMaxBotClient.
/// Все MAX-специфичные операции инкапсулированы здесь — команды ничего не знают о MAX.
/// Адаптер платформы MAX: реализует порт <see cref="IMessagingPlatform"/> через SDK <see cref="IMaxBotClient"/>.
/// Инкапсулирует всю работу с MAX API, ограждая доменную бизнес-логику от специфики платформы.
/// </summary>
public sealed class MaxPlatformAdapter : IMessagingPlatform
{
    public string Name { get; init; } = "Max";

    private readonly IMaxBotClient _client;
    private readonly ILogger<MaxPlatformAdapter> _logger;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="MaxPlatformAdapter"/>.
    /// </summary>
    /// <param name="client">Клиент MAX Bot API.</param>
    /// <param name="logger">Логгер сервиса.</param>
    public MaxPlatformAdapter(IMaxBotClient client, ILogger<MaxPlatformAdapter> logger)
    {
        _client = client;
        _logger = logger;
    }

    /// <inheritdoc/>
    public event Func<IncomingMessage, Task>? OnMessageReceived;

    /// <inheritdoc/>
    /// <summary>
    /// Отправляет текстовое сообщение в указанный чат MAX с поддержкой Markdown и fallback на обычный текст.
    /// </summary>
    /// <param name="chatId">Идентификатор чата MAX.</param>
    /// <param name="text">Текст сообщения.</param>
    /// <param name="keyboard">Клавиатура кнопок (опционально).</param>
    /// <param name="ct">Токен отмены.</param>
    public async Task SendTextAsync(string chatId, string text, BotKeyboard? keyboard = null, CancellationToken ct = default)
    {
        if (!long.TryParse(chatId, out var chatIdLong) || chatIdLong == 0)
        {
            _logger.LogError("Некорректный chatId");
            return;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            _logger.LogError("Пустой текст сообщения");
            return;
        }

        var request = new SendMessageRequest
        {
            ChatId = chatIdLong,
            Text = text,
            Format = MessageFormat.Markdown,
            Attachments = keyboard is not null
                ? new List<Attachment> { ButtonMapper.ToInlineKeyboardAttachment(keyboard) }
                : null
        };

        try
        {
            // Пробуем отправить сообщение с Markdown разметкой
            await _client.Messages.SendMessageAsync(request, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ошибка отправки сообщения с Markdown. Повтор без разметки.");
            try
            {
                // Если отправка с Markdown не удалась, пробуем отправить как обычный текст
                request.Format = null;
                await _client.Messages.SendMessageAsync(request, cancellationToken: ct);
            }
            catch (Exception innerEx)
            {
                _logger.LogError(innerEx, "Не удалось отправить сообщение даже без Markdown");
            }
        }
    }

    /// <inheritdoc/>
    /// <summary>
    /// Отправляет гео-локацию (координаты) в указанный чат MAX.
    /// </summary>
    /// <param name="chatId">Идентификатор чата.</param>
    /// <param name="latitude">Широта.</param>
    /// <param name="longitude">Долгота.</param>
    /// <param name="ct">Токен отмены.</param>
    public async Task SendLocationAsync(string chatId, double latitude, double longitude, CancellationToken ct = default)
    {
        if (!long.TryParse(chatId, out var chatIdLong) || chatIdLong == 0)
        {
            _logger.LogError("Некорректный chatId для отправки геопозиции");
            return;
        }

        if (double.IsNaN(latitude) || double.IsNaN(longitude))
        {
            // Проверка на валидность чисел
            _logger.LogError("Некорректные координаты для отправки геопозиции: {Latitude}, {Longitude}", latitude, longitude);
            return;
        }

        if (latitude is < -90 or > 90 || longitude is < -180 or > 180)
        {
            // Проверка на вхождение в диапазон допустимых значений
            _logger.LogError("Некорректные координаты для отправки геопозиции: {Latitude}, {Longitude}", latitude, longitude);
            return;
        }

        var request = new SendMessageRequest
        {
            ChatId = chatIdLong,
            Attachments = new List<Attachment>
            {
                new LocationAttachment(latitude, longitude)
            }
        };

        try
        {
            await _client.Messages.SendMessageAsync(request, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при отправке геопозиции ");
        }
    }

    /// <summary>
    /// Точка входа для polling-воркера — маппит Update в IncomingMessage и публикует событие.
    /// </summary>
    public async Task HandleUpdateAsync(Update update)
    {
        try
        {
            // Преобразуем специфичное для MAX событие в универсальную модель IncomingMessage
            var incoming = MapUpdate(update);
            if (incoming is null)
                return;

            // Публикуем событие о новом сообщении, на которое подписывается MaxPollingWorker
            if (OnMessageReceived is { } handler)
                await handler.Invoke(incoming);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при обработке события MAX update ({UpdateType})", update?.UpdateType);
        }
    }

    /// <summary>
    /// Преобразует объект события <see cref="Update"/> из MAX SDK в универсальную доменную модель <see cref="IncomingMessage"/>.
    /// </summary>
    /// <param name="update">Обновление от MAX Bot API.</param>
    /// <returns>Экземпляр <see cref="IncomingMessage"/> или null, если тип обновления не поддерживается.</returns>
    private IncomingMessage? MapUpdate(Update update)
    {
        // Определяем тип события от MAX и маппим его в нашу внутреннюю модель
        return update switch
        {
            // Новое сообщение в чате
            MessageCreatedUpdate created when created.Message?.Recipient is { ChatId: var chatId } => new IncomingMessage(
                ChatId: chatId.ToString(),
                UserId: created.Message.Sender?.Id.ToString() ?? chatId.ToString(),
                Text: created.Message.Body?.Text ?? string.Empty,
                CallbackData: null,
                Platform: Name),

            // Сообщение было отредактировано
            MessageEditedUpdate edited when edited.Message?.Recipient is { ChatId: var chatId } => new IncomingMessage(
                ChatId: chatId.ToString(),
                UserId: edited.Message.Sender?.Id.ToString() ?? chatId.ToString(),
                Text: edited.Message.Body?.Text ?? string.Empty,
                CallbackData: null,
                Platform: Name),

            // Нажатие на inline-кнопку (callback)
            MessageCallbackUpdate callbackUpdate when callbackUpdate.Callback?.Payload is { } payload => new IncomingMessage(
                ChatId: callbackUpdate.Message?.Recipient?.ChatId.ToString() ?? callbackUpdate.Callback.User?.Id.ToString() ?? string.Empty,
                UserId: callbackUpdate.Callback.User?.Id.ToString() ?? string.Empty,
                Text: payload,
                CallbackData: payload,
                Platform: Name),

            // Пользователь запустил бота (или диалог с ботом)
            BotStartedUpdate botStarted => new IncomingMessage(
                ChatId: botStarted.ChatId.ToString(),
                UserId: botStarted.User?.Id.ToString() ?? botStarted.ChatId.ToString(),
                Text: "/start",
                CallbackData: string.IsNullOrWhiteSpace(botStarted.Payload) ? null : botStarted.Payload,
                Platform: Name),

            // Все остальные типы обновлений игнорируем
            _ => null
        };
    }
}