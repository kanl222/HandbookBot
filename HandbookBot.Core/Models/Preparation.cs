namespace HandbookBot.Core.Models;

/// <summary>Остаток препарата в конкретном аптечном пункте.</summary>
public sealed record PreparationStock(
    int PharmacyId,
    string PharmacyName,
    string Address,
    decimal PackQty,
    string? Series = null,
    DateOnly? ExpirationDate = null);

/// <summary>Лекарственный препарат.</summary>
public sealed record Preparation(
    int Id,
    string Name,
    decimal Price,
    int PharmacyId,
    bool IsAvailable,
    string Description = "",
    IReadOnlyList<int>? PharmacyIds = null,
    string Manufacturer = "",
    string Dosage = "",
    decimal TotalPacks = 0m,
    string? Series = null,
    DateOnly? ExpirationDate = null,
    IReadOnlyList<PreparationStock>? Stocks = null)
{
    public IReadOnlyList<int> AvailablePharmacyIds =>
        Stocks is { Count: > 0 }
            ? Stocks.Select(s => s.PharmacyId).Distinct().ToList()
            : (PharmacyIds ?? (PharmacyId > 0 ? [PharmacyId] : []));

    public IReadOnlyList<PreparationStock> AvailableStocks =>
        Stocks ?? [];
}
