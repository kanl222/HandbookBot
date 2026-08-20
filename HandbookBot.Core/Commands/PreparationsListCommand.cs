using HandbookBot.Core.Interfaces;
using HandbookBot.Core.Models;

namespace HandbookBot.Core.Commands;

/// <summary>
/// Команда "preparations" — пагинированный список препаратов,
/// отсортированных по убыванию цены. Callback-формат: "preparations:{page}".
/// </summary>
public sealed class PreparationsListCommand : IBotCommand
{
    private const int PageSize = 5;

    private readonly IPreparationRepository _repo;

    public PreparationsListCommand(IPreparationRepository repo) => _repo = repo;

    public string Name => "preparations";

    public async Task ExecuteAsync(BotContext context, IncomingMessage message, CancellationToken ct = default)
    {
        // Определяем страницу из payload callback или из текста
        int page = 1;
        if (!string.IsNullOrEmpty(message.CallbackData))
        {
            var parts = message.CallbackData.Split(':', 2);
            if (parts.Length == 2) int.TryParse(parts[1], out page);
        }
        page = Math.Max(1, page);

        var result = await _repo.GetPageAsync(page, PageSize, ct);

        if (result.Items.Count == 0)
        {
            await context.ReplyAsync(
                "Список препаратов пуст.",
                BotKeyboard.SingleColumn(BotButton.Callback("Главное меню", "start:menu")));
            return;
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"*Препараты* (стр. {result.Page}/{result.TotalPages}):\n");

        for (var i = 0; i < result.Items.Count; i++)
        {
            var p = result.Items[i];
            var num = i + 1;

            var statusDetail = p.IsAvailable
                ? (p.AvailablePharmacyIds.Count > 1
                    ? $"В наличии ({p.AvailablePharmacyIds.Count} аптеки)"
                    : "В наличии")
                : "Нет в наличии";

            var summary = !string.IsNullOrWhiteSpace(p.Manufacturer)
                ? $"Производитель: {p.Manufacturer} | {statusDetail}"
                : $"Статус: {statusDetail}";

            sb.AppendLine($"{num}. *{EscapeMarkdown(p.Name)}*");
            sb.AppendLine($"   {EscapeMarkdown(summary)}");
            sb.AppendLine();
        }

        // Строим клавиатуру: кнопки перехода к подробному описанию препаратов (1, 2, 3...)
        var rows = new List<IReadOnlyList<BotButton>>();

        var prepButtons = new List<BotButton>();
        for (var i = 0; i < result.Items.Count; i++)
        {
            var p = result.Items[i];
            var num = i + 1;
            prepButtons.Add(BotButton.Callback($"{num}", $"prepinfo:{p.Id}"));
        }
        if (prepButtons.Count > 0)
        {
            rows.Add(prepButtons);
        }

        // Пагинация
        var navRow = new List<BotButton>();
        if (result.Page > 1)
            navRow.Add(BotButton.Callback("Назад", $"preparations:{result.Page - 1}"));
        if (result.Page < result.TotalPages)
            navRow.Add(BotButton.Callback("Вперёд", $"preparations:{result.Page + 1}"));
        if (navRow.Count > 0) rows.Add(navRow);

        // Кнопка поиска
        rows.Add(new[] { BotButton.Callback("Поиск препарата", "prepsearch:begin") });
        rows.Add(new[] { BotButton.Callback("Главное меню", "start:menu") });

        var keyboard = new BotKeyboard(rows);
        await context.ReplyAsync(sb.ToString(), keyboard);
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
