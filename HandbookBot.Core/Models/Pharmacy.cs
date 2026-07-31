namespace HandbookBot.Core.Models;

/// <summary>Аптечный пункт.</summary>
public sealed record Pharmacy(
    int Id,
    string Name,
    string Address,
    double Latitude,
    double Longitude,
    string Contact);
