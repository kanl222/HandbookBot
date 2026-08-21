using HandbookBot.Core.Interfaces;
using HandbookBot.Core.Models;

namespace HandbookBot.Core.Commands;

/// <summary>
/// Команда "prepinfo" — выводит подробную информацию и описание лекарственного препарата.
/// Вызывается через callback "prepinfo:{preparationId}".
/// </summary>
public sealed class PreparationDetailCommand : IBotCommand
{
    private readonly IPreparationRepository _preparationRepo;
    private readonly IPharmacyRepository _pharmacyRepo;

    public PreparationDetailCommand(IPreparationRepository preparationRepo, IPharmacyRepository pharmacyRepo)
    {
        _preparationRepo = preparationRepo;
        _pharmacyRepo = pharmacyRepo;
    }

    public string Name => "prepinfo";

    public async Task ExecuteAsync(BotContext context, IncomingMessage message, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(message.CallbackData))
            return;

        var parts = message.CallbackData.Split(':', 2);
        if (parts.Length < 2 || !int.TryParse(parts[1], out var prepId))
        {
            await context.ReplyOrEditAsync(
                message,
                "Некорректный идентификатор препарата.",
                BotKeyboard.SingleColumn(BotButton.Callback("Главное меню", "start:menu")));
            return;
        }

        var prep = await _preparationRepo.GetByIdAsync(prepId, ct);
        if (prep is null)
        {
            await context.ReplyOrEditAsync(
                message,
                "Препарат не найден.",
                BotKeyboard.SingleColumn(
                    BotButton.Callback("Список препаратов", "preparations:1"),
                    BotButton.Callback("Главное меню", "start:menu")));
            return;
        }

        var pharmacy = await _pharmacyRepo.GetByIdAsync(prep.PharmacyId, ct);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"*{EscapeMarkdown(prep.Name)}*\n");

        if (!string.IsNullOrWhiteSpace(prep.Manufacturer))
            sb.AppendLine($"• Производитель: {EscapeMarkdown(prep.Manufacturer)}");

        var dosage = !string.IsNullOrWhiteSpace(prep.Dosage) ? prep.Dosage : prep.Description;
        if (!string.IsNullOrWhiteSpace(dosage))
            sb.AppendLine($"• Форма / дозировка: {EscapeMarkdown(dosage)}");

        if (prep.IsAvailable && prep.TotalPacks > 0)
        {
            sb.AppendLine($"• Статус: В наличии (всего {prep.TotalPacks:0.##} уп.)");

            var seriaStr = !string.IsNullOrWhiteSpace(prep.Series) ? $"Серия: {prep.Series}" : string.Empty;
            var expStr = prep.ExpirationDate.HasValue ? $" (годен до {prep.ExpirationDate.Value:dd.MM.yyyy})" : string.Empty;
            if (!string.IsNullOrEmpty(seriaStr) || !string.IsNullOrEmpty(expStr))
            {
                sb.AppendLine($"• {EscapeMarkdown((seriaStr + expStr).Trim())}");
            }

            if (prep.AvailableStocks.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Наличие в аптечных пунктах:");
                foreach (var stock in prep.AvailableStocks)
                {
                    var addr = !string.IsNullOrWhiteSpace(stock.Address) ? $" ({stock.Address})" : string.Empty;
                    sb.AppendLine($"• {EscapeMarkdown(stock.PharmacyName)}{EscapeMarkdown(addr)} — {stock.PackQty:0.##} уп.");
                }
            }
        }
        else
        {
            sb.AppendLine("• Статус: Нет в наличии в аптечных пунктах");
        }

        var rows = new List<IReadOnlyList<BotButton>>();

        // Кнопки для каждого аптечного пункта с наличием
        var pharmacyIds = prep.AvailablePharmacyIds;
        foreach (var phId in pharmacyIds)
        {
            var ph = await _pharmacyRepo.GetByIdAsync(phId, ct);
            var label = ph is not null ? $"Карта: {ph.Name}" : $"Аптечный пункт №{phId}";
            rows.Add([BotButton.Callback(label, $"pharmmap:{phId}")]);
        }

        // Навигационные кнопки
        rows.Add([BotButton.Callback("Все аптечные пункты", "pharmacies:1")]);
        rows.Add([BotButton.Callback("Список препаратов", "preparations:1")]);
        rows.Add([BotButton.Callback("Главное меню", "start:menu")]);

        var keyboard = new BotKeyboard(rows);
        await context.ReplyOrEditAsync(message, sb.ToString(), keyboard);
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
