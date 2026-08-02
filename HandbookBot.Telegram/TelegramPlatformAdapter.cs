using HandbookBot.Core.Interfaces;
using HandbookBot.Core.Models;
using HandbookBot.Telegram.Mapping;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace HandbookBot.Telegram;

/// <summary>
/// Адаптер платформы Telegram: реализует порт <see cref="IMessagingPlatform"/> через SDK Telegram.Bot.
/// Инкапсулирует всю работу с Telegram API, ограждая доменную бизнес-логику от специфики Telegram.
/// </summary>
public sealed class TelegramPlatformAdapter : IMessagingPlatform
{

    public string Name { get; init; } = "Telegram";
    private readonly ITelegramBotClient _client;
    private readonly ILogger<TelegramPlatformAdapter> _logger;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="TelegramPlatformAdapter"/>.
    /// </summary>
    /// <param name="client">Клиент Telegram Bot API.</param>
    /// <param name="logger">Логгер сервиса.</param>
    public TelegramPlatformAdapter(ITelegramBotClient client, ILogger<TelegramPlatformAdapter> logger)
    {
        _client = client;
        _logger = logger;
    }

    /// <inheritdoc />
    public event Func<IncomingMessage, Task>? OnMessageReceived;

    /// <summary>
    /// Отправляет текстовое сообщение в указанный чат Telegram с поддержкой Markdown и fallback на обычный текст.
    /// </summary>
    /// <param name="chatId">Идентификатор чата Telegram.</param>
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

        var markup = keyboard is not null ? ButtonMapper.Create(keyboard) : null;

        try
        {
            await _client.SendMessage(
                chatId: chatIdLong,
                text: text,
                parseMode: ParseMode.Markdown,
                replyMarkup: markup,
                cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ошибка отправки сообщения с Markdown. Выполняется повторная отправка без разметки.");
            try
            {
                await _client.SendMessage(
                    chatId: chatIdLong,
                    text: text,
                    replyMarkup: markup,
                    cancellationToken: ct);
            }
            catch (Exception innerEx)
            {
                _logger.LogError(innerEx, "Не удалось отправить сообщение даже без разметки Markdown");
            }
        }
    }

    /// <summary>
    /// Отправляет гео-локацию (координаты) в указанный чат Telegram.
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

        try
        {
            await _client.SendLocation(
                chatId: chatIdLong,
                latitude: (float)latitude,
                longitude: (float)longitude,
                cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при отправке геопозиции");
        }
    }

    /// <summary>
    /// Преобразует объект события <see cref="Update"/> из Telegram SDK в универсальную доменную модель <see cref="IncomingMessage"/>.
    /// </summary>
    /// <param name="update">Обновление от Telegram Bot API.</param>
    /// <returns>Экземпляр <see cref="IncomingMessage"/> или null, если тип обновления не поддерживается.</returns>
    public IncomingMessage? MapUpdate(Update update)
    {
        return update.Type switch
        {
            UpdateType.Message when update.Message?.Text is not null => new IncomingMessage(
                ChatId: update.Message.Chat.Id.ToString(),
                UserId: update.Message.From?.Id.ToString() ?? update.Message.Chat.Id.ToString(),
                Text: update.Message.Text,
                CallbackData: null,
                Platform: Name),

            UpdateType.CallbackQuery when update.CallbackQuery is not null => new IncomingMessage(
                ChatId: GetCallbackChatId(update.CallbackQuery),
                UserId: update.CallbackQuery.From.Id.ToString(),
                Text: update.CallbackQuery.Data ?? string.Empty,
                CallbackData: update.CallbackQuery.Data,
                Platform: Name),

            _ => null
        };
    }

    /// <summary>
    /// Отправляет ответ на CallbackQuery, чтобы убрать состояние загрузки на кнопках Telegram UI.
    /// </summary>
    /// <param name="update">Обновление Telegram.</param>
    public async Task AcknowledgeCallbackAsync(Update update)
    {
        if (update.Type == UpdateType.CallbackQuery && update.CallbackQuery is not null)
        {
            try
            {
                await _client.AnswerCallbackQuery(update.CallbackQuery.Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Не удалось выполнить AnswerCallbackQuery");
            }
        }
    }

    /// <summary>
    /// Извлекает ИД чата из CallbackQuery.
    /// </summary>
    private static string GetCallbackChatId(CallbackQuery query)
    {
        if (query.Message is { } msg && msg.Chat.Id != 0)
        {
            return msg.Chat.Id.ToString();
        }
        return query.From.Id.ToString();
    }
}
