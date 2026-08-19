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
            await context.ReplyAsync(
                "Некорректный идентификатор препарата.",
                BotKeyboard.SingleColumn(BotButton.Callback("Главное меню", "start:menu")));
            return;
        }

        var prep = await _preparationRepo.GetByIdAsync(prepId, ct);
        if (prep is null)
        {
            await context.ReplyAsync(
                "Препарат не найден.",
                BotKeyboard.SingleColumn(
                    BotButton.Callback("Список препаратов", "preparations:1"),
                    BotButton.Callback("Главное меню", "start:menu")));
            return;
        }

        var pharmacy = await _pharmacyRepo.GetByIdAsync(prep.PharmacyId, ct);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"*{EscapeMarkdown(prep.Name)}*\n");

        if (!string.IsNullOrWhiteSpace(prep.Description))
        {
            sb.AppendLine($"*Описание:*\n{EscapeMarkdown(prep.Description)}\n");
        }

        var status = prep.IsAvailable ? "В наличии" : "Нет в наличии";
        sb.AppendLine($"*Цена:* {prep.Price:N2} руб. | {status}");

        if (pharmacy is not null)
        {
            sb.AppendLine($"*Аптечный пункт:* {EscapeMarkdown(pharmacy.Name)}");
            sb.AppendLine($"*Адрес:* {EscapeMarkdown(pharmacy.Address)}");
        }

        var keyboard = BotKeyboard.SingleColumn(
            BotButton.Callback("Аптечный пункт", $"pharmmap:{prep.PharmacyId}"),
            BotButton.Callback("Главное меню", "start:menu"));

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
