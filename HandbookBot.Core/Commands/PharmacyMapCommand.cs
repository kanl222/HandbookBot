using HandbookBot.Core.Interfaces;
using HandbookBot.Core.Models;

namespace HandbookBot.Core.Commands;

/// <summary>
/// Вспомогательная команда "pharmmap" — отправляет геолокацию аптеки по её Id.
/// Вызывается через callback "pharmmap:{pharmacyId}".
/// </summary>
public sealed class PharmacyMapCommand : IBotCommand
{
    private readonly IPharmacyRepository _repo;

    public PharmacyMapCommand(IPharmacyRepository repo) => _repo = repo;

    public string Name => "pharmmap";

    public async Task ExecuteAsync(BotContext context, IncomingMessage message, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(message.CallbackData))
            return;

        var parts = message.CallbackData.Split(':');
        if (parts.Length < 2 || !int.TryParse(parts[1], out var pharmacyId))
        {
            await context.ReplyOrEditAsync(
                message,
                "Некорректный идентификатор аптеки.",
                BotKeyboard.SingleColumn(BotButton.Callback("Главное меню", "start:menu")));
            return;
        }

        int? prepId = parts.Length > 2 && int.TryParse(parts[2], out var parsedPrepId) ? parsedPrepId : null;
        var source = parts.Length > 3 ? parts[3] : "none";
        var sourcePage = parts.Length > 4 && int.TryParse(parts[4], out var p) ? p : 1;

        var pharmacy = await _repo.GetByIdAsync(pharmacyId, ct);

        if (pharmacy is null)
        {
            await context.ReplyOrEditAsync(
                message,
                "Аптека не найдена.",
                BotKeyboard.SingleColumn(
                    BotButton.Callback("Аптечные пункты", "pharmacies:1"),
                    BotButton.Callback("Главное меню", "start:menu")), ct);
            return;
        }

        var buttons = new List<BotButton>();
        if (prepId.HasValue)
        {
            buttons.Add(BotButton.Callback("Назад к препарату", $"prepinfo:{prepId.Value}:{source}:{sourcePage}"));
        }
        buttons.Add(BotButton.Callback("Аптечные пункты", "pharmacies:1"));
        buttons.Add(BotButton.Callback("Главное меню", "start:menu"));

        await context.SendLocationAsync(pharmacy.Latitude, pharmacy.Longitude);
        await context.ReplyAsync(
            $"*{pharmacy.Name}*\n{pharmacy.Address}\nТел: {pharmacy.Contact}",
            BotKeyboard.SingleColumn(buttons.ToArray()), ct);
    }
}
