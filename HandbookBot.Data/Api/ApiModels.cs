using HandbookBot.Core.Models;
using System.Text.Json.Serialization;

namespace HandbookBot.Data.Api;

// ──────────────────────────── Pagination ────────────────────────────

/// <summary>
/// Универсальная обёртка пагинированного ответа RefInfoAPI.
/// JSON: { "items": [...], "totalCount": 42, "page": 1, "pageSize": 5 }
/// </summary>
public sealed class ApiPagedResult<T>
{
    [JsonPropertyName("items")]
    public List<T> Items { get; set; } = [];

    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }

    [JsonPropertyName("page")]
    public int Page { get; set; } = 1;

    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; } = 5;

    public PagedResult<TDomain> ToDomain<TDomain>(int page, int pageSize, Func<T, TDomain> map)
        => new(Items.Select(map).ToList().AsReadOnly(), TotalCount, page, pageSize);
}

public static class ApiPagedResultExtensions
{
    public static PagedResult<Preparation> ToDomain(this ApiPagedResult<ApiPreparation> result, int page, int pageSize)
        => result.ToDomain(page, pageSize, x => x.ToDomain());

    public static PagedResult<Pharmacy> ToDomain(this ApiPagedResult<ApiPharmacy> result, int page, int pageSize)
        => result.ToDomain(page, pageSize, x => x.ToDomain());
}

// ──────────────────────────── Drug → Preparation ────────────────────────────

/// <summary>DTO остатка препарата в аптечном пункте из RefInfoAPI.</summary>
public sealed class ApiDrugStock
{
    [JsonPropertyName("drugStoreId")]
    public int DrugStoreId { get; set; }

    [JsonPropertyName("drugStoreName")]
    public string DrugStoreName { get; set; } = string.Empty;

    [JsonPropertyName("address")]
    public string Address { get; set; } = string.Empty;

    [JsonPropertyName("packQty")]
    public decimal PackQty { get; set; }

    [JsonPropertyName("series")]
    public string? Series { get; set; }

    [JsonPropertyName("expirationDate")]
    public DateOnly? ExpirationDate { get; set; }

    public PreparationStock ToDomain() => new(DrugStoreId, DrugStoreName, Address, PackQty, Series, ExpirationDate);
}

/// <summary>
/// DTO препарата из RefInfoAPI (GET /api/drugs).
/// </summary>
public sealed class ApiPreparation
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("manufacturer")]
    public string Manufacturer { get; set; } = string.Empty;

    [JsonPropertyName("dosage")]
    public string Dosage { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("price")]
    public decimal Price { get; set; }

    [JsonPropertyName("drugStoreId")]
    public int DrugStoreId { get; set; }

    [JsonPropertyName("drugStoreIds")]
    public List<int> DrugStoreIds { get; set; } = [];

    [JsonPropertyName("isAvailable")]
    public bool IsAvailable { get; set; }

    [JsonPropertyName("totalPacks")]
    public decimal TotalPacks { get; set; }

    [JsonPropertyName("series")]
    public string? Series { get; set; }

    [JsonPropertyName("expirationDate")]
    public DateOnly? ExpirationDate { get; set; }

    [JsonPropertyName("stocks")]
    public List<ApiDrugStock> Stocks { get; set; } = [];

    public Preparation ToDomain() => new(
        Id,
        Name,
        Price,
        DrugStoreId,
        IsAvailable,
        Description,
        DrugStoreIds.Count > 0 ? DrugStoreIds : (DrugStoreId > 0 ? [DrugStoreId] : []),
        Manufacturer,
        Dosage,
        TotalPacks,
        Series,
        ExpirationDate,
        Stocks.Select(s => s.ToDomain()).ToList());
}

// ──────────────────────────── DrugStore → Pharmacy ────────────────────────────

/// <summary>
/// DTO аптечного пункта из RefInfoAPI (GET /api/drugstores).
/// JSON: { "id", "name", "address", "latitude", "longitude", "contact" }
/// </summary>
public sealed class ApiPharmacy
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("address")]
    public string Address { get; set; } = string.Empty;

    [JsonPropertyName("latitude")]
    public double Latitude { get; set; }

    [JsonPropertyName("longitude")]
    public double Longitude { get; set; }

    [JsonPropertyName("contact")]
    public string Contact { get; set; } = string.Empty;

    public Pharmacy ToDomain() => new(Id, Name, Address, Latitude, Longitude, Contact);
}

// ──────────────────────────── FAQ ────────────────────────────

/// <summary>
/// DTO записи FAQ из RefInfoAPI (GET /api/faq).
/// JSON: { "id", "question", "answer" }
/// </summary>
public sealed class ApiFaqEntry
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("question")]
    public string Question { get; set; } = string.Empty;

    [JsonPropertyName("answer")]
    public string Answer { get; set; } = string.Empty;

    public FaqEntry ToDomain() => new(Id, Question, Answer);
}
