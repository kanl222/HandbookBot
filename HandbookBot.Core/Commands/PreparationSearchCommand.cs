using HandbookBot.Core.Interfaces;
using HandbookBot.Core.Models;

namespace HandbookBot.Core.Commands;

/// <summary>
/// Команда "prepsearch" — двухшаговый диалог поиска препарата.
/// Шаг 1 (callback "prepsearch:begin"): переводим пользователя в режим ожидания ввода.
/// Шаг 2 (сессионный ввод): выполняем поиск по введённому тексту.
/// Callback "prepsearch:{page}:{query}": пагинация результатов поиска.
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
        // Шаг 1: начало поиска по callback "prepsearch:begin"
        if (!string.IsNullOrEmpty(message.CallbackData))
        {
            var parts = message.CallbackData.Split(':', 3);
            var action = parts.Length >= 2 ? parts[1] : string.Empty;

            if (action == "begin")
            {
                await context.Sessions.SetStateAsync(
                    context.UserId,
                    new UserDialogState(AwaitingKey),
                    TimeSpan.FromMinutes(5),
                    ct);

                await context.ReplyAsync(
                    "Введите название препарата или его часть для поиска:",
                    BotKeyboard.SingleColumn(BotButton.Callback("Отмена", "start:menu")));
                return;
            }

            // Пагинация: "prepsearch:{page}:{query}"
            if (parts.Length == 3 && int.TryParse(action, out var pg))
            {
                var query = parts[2];
                await ShowResultsAsync(context, query, pg, ct);
                return;
            }
        }

        // Шаг 2: сессионный ввод — текст от пользователя
        await context.Sessions.ClearStateAsync(context.UserId, ct);
        var searchText = message.Text?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(searchText))
        {
            await context.ReplyAsync("Поисковый запрос не может быть пустым. Попробуйте ещё раз или нажмите /start.");
            return;
        }

        await ShowResultsAsync(context, searchText, 1, ct);
    }

    private async Task ShowResultsAsync(BotContext context, string query, int page, CancellationToken ct)
    {
        var result = await _repo.SearchAsync(query, page, PageSize, ct);

        if (result.TotalCount == 0)
        {
            await context.ReplyAsync(
                $"По запросу «{query}» ничего не найдено.",
                BotKeyboard.SingleColumn(
                    BotButton.Callback("Новый поиск", "prepsearch:begin"),
                    BotButton.Callback("Главное меню", "start:menu")));
            return;
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Результаты поиска «{query}» (стр. {result.Page}/{result.TotalPages}, всего {result.TotalCount}):\n");

        foreach (var p in result.Items)
        {
            var status = p.IsAvailable ? "В наличии" : "Нет в наличии";
            sb.AppendLine($"*{p.Name}* — {p.Price:N2} руб. ({status})");
        }

        var rows = new List<IReadOnlyList<BotButton>>();
        var navRow = new List<BotButton>();
        if (result.Page > 1)
            navRow.Add(BotButton.Callback("Назад", $"prepsearch:{result.Page - 1}:{query}"));
        if (result.Page < result.TotalPages)
            navRow.Add(BotButton.Callback("Вперёд", $"prepsearch:{result.Page + 1}:{query}"));
        if (navRow.Count > 0) rows.Add(navRow);

        rows.Add(new[] { BotButton.Callback("Новый поиск", "prepsearch:begin") });
        rows.Add(new[] { BotButton.Callback("Главное меню", "start:menu") });

        await context.ReplyAsync(sb.ToString(), new BotKeyboard(rows));
    }
}
