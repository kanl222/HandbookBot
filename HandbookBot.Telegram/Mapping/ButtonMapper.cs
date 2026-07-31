using System;
using System.Linq;
using HandbookBot.Core.Models;
using Telegram.Bot.Types.ReplyMarkups;

namespace HandbookBot.Telegram.Mapping;

/// <summary>
/// Класс-маппер для конвертации доменной абстрактной клавиатуры <see cref="BotKeyboard"/> 
/// в Telegram-специфичную инлайн-клавиатуру <see cref="InlineKeyboardMarkup"/>.
/// </summary>
internal static class ButtonMapper
{
    /// <summary>
    /// Создает экземпляр <see cref="InlineKeyboardMarkup"/> на основе универсальной клавиатуры <see cref="BotKeyboard"/>.
    /// </summary>
    /// <param name="keyboard">Доменная модель клавиатуры.</param>
    /// <returns>Инлайн-клавиатура Telegram.</returns>
    public static InlineKeyboardMarkup Create(BotKeyboard keyboard)
    {
        var rows = keyboard.Rows.Select(row =>
            row.Select(MapButton).ToArray()
        ).ToArray();

        return new InlineKeyboardMarkup(rows);
    }

    /// <summary>
    /// Преобразует одну доменную кнопку <see cref="BotButton"/> в инлайн-кнопку Telegram <see cref="InlineKeyboardButton"/>.
    /// </summary>
    /// <param name="button">Доменная кнопка.</param>
    /// <returns>Инлайн-кнопка Telegram.</returns>
    /// <exception cref="NotSupportedException">Выбрасывается для неинлайн типов кнопок (например, запрос геопозиции).</exception>
    /// <exception cref="ArgumentOutOfRangeException">Выбрасывается при неизвестном типе кнопки.</exception>
    private static InlineKeyboardButton MapButton(BotButton button) => button.Type switch
    {
        BotButtonType.Callback => InlineKeyboardButton.WithCallbackData(button.Text, button.Payload ?? string.Empty),
        BotButtonType.Url => InlineKeyboardButton.WithUrl(button.Text, button.Url ?? "#"),
        BotButtonType.RequestGeo => throw new NotSupportedException(
            "BotButtonType.RequestGeo не поддерживается в Telegram InlineKeyboardMarkup. " +
            "Используйте ReplyKeyboardMarkup с KeyboardButton.WithRequestLocation() вместо inline-клавиатуры."),
        BotButtonType.RequestText => throw new NotSupportedException(
            "BotButtonType.RequestText не поддерживается в InlineKeyboardMarkup."),
        _ => throw new ArgumentOutOfRangeException(nameof(button.Type), button.Type, "Неизвестный тип кнопки.")
    };
}
