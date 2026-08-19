namespace HandbookBot.Core.Models;

/// <summary>Лекарственный препарат.</summary>
public sealed record Preparation(
    int Id,
    string Name,
    decimal Price,
    int PharmacyId,
    bool IsAvailable,
    string Description = "");
