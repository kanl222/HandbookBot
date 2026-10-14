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
                BotKeyboard.SingleColumn(BotButton.Callback("Главное меню", "start:menu")), ct: ct);
            return;
        }

        var pharmacy = await _repo.GetByIdAsync(pharmacyId, ct);

        if (pharmacy is null)
        {
            await context.ReplyAsync(
                "Аптека не найдена.",
                BotKeyboard.SingleColumn(
                    BotButton.Callback("Аптечные пункты", "pharmacies:1"),
                    BotButton.Callback("Главное меню", "start:menu")), ct: ct);
            return;
        }
        var hasCoordinates = double.IsFinite(pharmacy.Latitude) && double.IsFinite(pharmacy.Longitude)
            && pharmacy.Latitude is >= -90 and <= 90
            && pharmacy.Longitude is >= -180 and <= 180
            && (pharmacy.Latitude != 0 || pharmacy.Longitude != 0);
        if (hasCoordinates)
            await context.SendLocationAsync(pharmacy.Latitude, pharmacy.Longitude, ct: ct);
        await context.ReplyAsync(
            $"*{pharmacy.Name}*\n{pharmacy.Address}\nТел: {pharmacy.Contact}"
                + (hasCoordinates ? string.Empty : "\nКоординаты аптеки пока недоступны. Используйте адрес для поиска на карте."),
            BotKeyboard.SingleColumn(
                BotButton.Callback("Аптечные пункты", "pharmacies:1"),
                BotButton.Callback("Главное меню", "start:menu")), ct: ct);
    }
}
