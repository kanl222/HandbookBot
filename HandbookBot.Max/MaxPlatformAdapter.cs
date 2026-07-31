using HandbookBot.Core.Interfaces;
using HandbookBot.Core.Models;
using HandbookBot.Max.Mapping;
using MAX.Bot.Interfaces;
using MAX.Bot.Interfaces.Models;
using MAX.Bot.Interfaces.Models.Request.Message;
using MAX.Bot.Interfaces.Models.Request.Message.Attachment;
using MAX.Bot.Interfaces.Models.Request.Message.Attachment.Payloads;
using Microsoft.Extensions.Logging;

namespace HandbookBot.Max;

/// <summary>
/// Адаптер MAX-платформы: реализует IMessagingPlatform через IMaxBotClient.
/// Все MAX-специфичные операции инкапсулированы здесь — команды ничего не знают о MAX.
/// </summary>
public sealed class MaxPlatformAdapter : IMessagingPlatform
{
    private readonly IMaxBotClient _client;
    private readonly ILogger<MaxPlatformAdapter> _logger;

    public MaxPlatformAdapter(IMaxBotClient client, ILogger<MaxPlatformAdapter> logger)
    {
        _client = client;
        _logger = logger;
    }

    /// <inheritdoc/>
    public event Func<IncomingMessage, Task>? OnMessageReceived;

    /// <inheritdoc/>
    public async Task SendTextAsync(string chatId, string text, BotKeyboard? keyboard = null, CancellationToken ct = default)
    {
        if (!long.TryParse(chatId, out var chatIdLong) || chatIdLong == 0)
        {
            _logger.LogError("Некорректный chatId");
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
            await _client.Messages.SendMessageAsync(request, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ошибка отправки сообщения с Markdown. Повтор без разметки.");
            try
            {
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
    public async Task SendLocationAsync(string chatId, double latitude, double longitude, CancellationToken ct = default)
    {
        if (!long.TryParse(chatId, out var chatIdLong) || chatIdLong == 0)
        {
            _logger.LogError("Некорректный chatId для отправки геопозиции");
            return;
        }

        var request = new SendMessageRequest
        {
            ChatId = chatIdLong,
            Attachments = new List<Attachment>
            {
                new LocationAttachment
                {
                    Payload = new LocationPayload
                    {
                        Latitude = latitude,
                        Longitude = longitude
                    }
                }
            }
        };

        try
        {
            await _client.Messages.SendMessageAsync(request, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при отправке геопозиции");
        }
    }

    /// <summary>
    /// Точка входа для polling-воркера — маппит Update в IncomingMessage и публикует событие.
    /// </summary>
    public async Task HandleUpdateAsync(Update update)
    {
        try
        {
            var incoming = MapUpdate(update);
            if (incoming is null)
                return;

            if (OnMessageReceived is { } handler)
                await handler.Invoke(incoming);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при обработке события MAX update ({UpdateType})", update?.UpdateType);
        }
    }

    private IncomingMessage? MapUpdate(Update update)
    {
        return update switch
        {
            MessageCreatedUpdate created when created.Message?.Recipient is { ChatId: var chatId } => new IncomingMessage(
                ChatId: chatId.ToString(),
                UserId: created.Message.Sender?.Id.ToString() ?? chatId.ToString(),
                Text: created.Message.Body?.Text ?? string.Empty,
                CallbackData: null,
                Platform: "Max"),

            MessageEditedUpdate edited when edited.Message?.Recipient is { ChatId: var chatId } => new IncomingMessage(
                ChatId: chatId.ToString(),
                UserId: edited.Message.Sender?.Id.ToString() ?? chatId.ToString(),
                Text: edited.Message.Body?.Text ?? string.Empty,
                CallbackData: null,
                Platform: "Max"),

            MessageCallbackUpdate callbackUpdate when callbackUpdate.Callback?.Payload is { } payload => new IncomingMessage(
                ChatId: callbackUpdate.Message?.Recipient?.ChatId.ToString() ?? callbackUpdate.Callback.User?.Id.ToString() ?? string.Empty,
                UserId: callbackUpdate.Callback.User?.Id.ToString() ?? string.Empty,
                Text: payload,
                CallbackData: payload,
                Platform: "Max"),

            // BotStarted: Text — всегда "/start" (запускает команду start),
            // CallbackData — Payload из диплинка (если был передан при старте)
            BotStartedUpdate botStarted => new IncomingMessage(
                ChatId: botStarted.ChatId.ToString(),
                UserId: botStarted.User?.Id.ToString() ?? botStarted.ChatId.ToString(),
                Text: "/start",
                CallbackData: string.IsNullOrWhiteSpace(botStarted.Payload) ? null : botStarted.Payload,
                Platform: "Max"),

            _ => null
        };
    }
}