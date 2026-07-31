using HandbookBot.Core.Interfaces;
using HandbookBot.Core.Models;

namespace HandbookBot.Core.Commands;

/// <summary>
/// Команда "pharmacies" — пагинированный список аптечных пунктов.
/// Callback-формат: "pharmacies:{page}".
/// Для каждой аптеки — кнопка "Показать на карте" вызывающая "pharmmap:{id}".
/// </summary>
public sealed class PharmaciesListCommand : IBotCommand
{
    private const int PageSize = 3;

    private readonly IPharmacyRepository _repo;

    public PharmaciesListCommand(IPharmacyRepository repo) => _repo = repo;

    public string Name => "pharmacies";

    public async Task ExecuteAsync(BotContext context, IncomingMessage message, CancellationToken ct = default)
    {
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
            await context.ReplyAsync("Список аптечных пунктов пуст.");
            return;
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"*Аптечные пункты* (стр. {result.Page}/{result.TotalPages}):\n");

        foreach (var ph in result.Items)
        {
            sb.AppendLine($"*{ph.Name}*");
            sb.AppendLine($"Адрес: {ph.Address}");
            sb.AppendLine($"Тел: {ph.Contact}");
            sb.AppendLine();
        }

        var rows = new List<IReadOnlyList<BotButton>>();

        foreach (var ph in result.Items)
        {
            rows.Add(new[]
            {
                BotButton.Callback($"Карта: {ph.Name}", $"pharmmap:{ph.Id}")
            });
        }

        var navRow = new List<BotButton>();
        if (result.Page > 1)
            navRow.Add(BotButton.Callback("Назад", $"pharmacies:{result.Page - 1}"));
        if (result.Page < result.TotalPages)
            navRow.Add(BotButton.Callback("Вперёд", $"pharmacies:{result.Page + 1}"));
        if (navRow.Count > 0) rows.Add(navRow);

        rows.Add(new[] { BotButton.Callback("Главное меню", "start:menu") });

        await context.ReplyAsync(sb.ToString(), new BotKeyboard(rows));
    }
}
