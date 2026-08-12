using HandbookBot.Core.Interfaces;
using HandbookBot.Core.Models;

namespace HandbookBot.Core.Commands;

/// <summary>
/// Команда "prepsearch" — двухшаговый диалог поиска препарата.
/// Шаг 1 (callback "prepsearch:begin"): переводим пользователя в режим ожидания ввода.
/// Шаг 2 (сессионный ввод): сохраняем запрос в сессию и выполняем поиск.
/// Callback "prepsearch:{page}": пагинация результатов поиска с чтением запроса из сессии (обход ограничения 64 байт Telegram).
/// </summary>
public sealed class PreparationSearchCommand : IBotCommand
{
    private const int PageSize = 5;
    private const string AwaitingKey = "prepsearch";

    private readonly IPreparationRepository _repo;

    public PreparationSearchCommand(IPreparationRepository repo) => _repo = repo;

    public string Name => "prepsearch";

    public async Task ExecuteAsync(BotContext context, IncomingMessage message, CancellationToken ct = default)
    {
        // Шаг 1: начало поиска или пагинация по callback-данным
        if (!string.IsNullOrEmpty(message.CallbackData))
        {
            var parts = message.CallbackData.Split(':', 2);
            var action = parts.Length >= 2 ? parts[1] : string.Empty;

            if (action == "begin")
            {
                await context.Sessions.SetStateAsync(
                    context.UserId,
                    new UserDialogState(AwaitingKey),
                    TimeSpan.FromMinutes(10),
                    ct);

                await context.ReplyAsync(
                    "Введите название препарата или его часть для поиска:",
                    BotKeyboard.SingleColumn(BotButton.Callback("Отмена", "start:menu")));
                return;
            }

            // Пагинация: "prepsearch:{page}" (запрос извлекается из сессии)
            if (int.TryParse(action, out var pg))
            {
                var sessionState = await context.Sessions.GetStateAsync(context.UserId, ct);
                var query = sessionState?.SearchQuery;

                if (string.IsNullOrWhiteSpace(query))
                {
                    await context.ReplyAsync(
                        "Время сессии поиска истекло. Пожалуйста, введите запрос заново:",
                        BotKeyboard.SingleColumn(
                            BotButton.Callback("Новый поиск", "prepsearch:begin"),
                            BotButton.Callback("Главное меню", "start:menu")));
                    return;
                }

                await ShowResultsAsync(context, query, pg, ct);
                return;
            }
        }

        // Шаг 2: сессионный ввод — текст от пользователя
        var searchText = message.Text?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(searchText))
        {
            // Сессию не сбрасываем — пользователь остаётся в режиме ожидания ввода
            await context.ReplyAsync(
                "Поисковый запрос не может быть пустым. Введите название препарата:",
                BotKeyboard.SingleColumn(BotButton.Callback("Главное меню", "start:menu")));
            return;
        }

        // Сохраняем поисковый запрос в сессию для последующей пагинации
        await context.Sessions.SetStateAsync(
            context.UserId,
            new UserDialogState(AwaitingKey) { SearchQuery = searchText },
            TimeSpan.FromMinutes(10),
            ct);

        await ShowResultsAsync(context, searchText, 1, ct);
    }

    private async Task ShowResultsAsync(BotContext context, string query, int page, CancellationToken ct)
    {
        var result = await _repo.SearchAsync(query, page, PageSize, ct);

        if (result.TotalCount == 0)
        {
            var escapedQuery = EscapeMarkdown(query);
            await context.ReplyAsync(
                $"По запросу «{escapedQuery}» ничего не найдено.",
                BotKeyboard.SingleColumn(
                    BotButton.Callback("Новый поиск", "prepsearch:begin"),
                    BotButton.Callback("Главное меню", "start:menu")));
            return;
        }

        var escapedQueryTitle = EscapeMarkdown(query);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Результаты поиска «{escapedQueryTitle}» (стр. {result.Page}/{result.TotalPages}, всего {result.TotalCount}):\n");

        foreach (var p in result.Items)
        {
            var status = p.IsAvailable ? "В наличии" : "Нет в наличии";
            var escapedName = EscapeMarkdown(p.Name);
            sb.AppendLine($"*{escapedName}* — {p.Price:N2} руб. ({status})");
        }

        var rows = new List<IReadOnlyList<BotButton>>();
        var navRow = new List<BotButton>();
        if (result.Page > 1)
            navRow.Add(BotButton.Callback("Назад", $"prepsearch:{result.Page - 1}"));
        if (result.Page < result.TotalPages)
            navRow.Add(BotButton.Callback("Вперёд", $"prepsearch:{result.Page + 1}"));
        if (navRow.Count > 0) rows.Add(navRow);

        rows.Add(new[] { BotButton.Callback("Новый поиск", "prepsearch:begin") });
        rows.Add(new[] { BotButton.Callback("Главное меню", "start:menu") });

        await context.ReplyAsync(sb.ToString(), new BotKeyboard(rows));
    }

    private static string EscapeMarkdown(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        return text.Replace("_", "\\_")
                   .Replace("*", "\\*")
                   .Replace("[", "\\[")
                   .Replace("`", "\\`");
    }
}
