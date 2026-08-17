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

        var parts = message.CallbackData.Split(':', 2);
        if (parts.Length < 2 || !int.TryParse(parts[1], out var pharmacyId))
        {
            await context.ReplyAsync(
                "Некорректный идентификатор аптеки.",
                BotKeyboard.SingleColumn(BotButton.Callback("Главное меню", "start:menu")));
            return;
        }

        var pharmacy = await _repo.GetByIdAsync(pharmacyId, ct);

        if (pharmacy is null)
        {
            await context.ReplyAsync(
                "Аптека не найдена.",
                BotKeyboard.SingleColumn(
                    BotButton.Callback("Аптечные пункты", "pharmacies:1"),
                    BotButton.Callback("Главное меню", "start:menu")));
            return;
        }
        await context.SendLocationAsync(pharmacy.Latitude, pharmacy.Longitude);
        await context.ReplyAsync(
            $"*{pharmacy.Name}*\n{pharmacy.Address}\nТел: {pharmacy.Contact}",
            BotKeyboard.SingleColumn(
                BotButton.Callback("Аптечные пункты", "pharmacies:1"),
                BotButton.Callback("Главное меню", "start:menu")));
    }
}
