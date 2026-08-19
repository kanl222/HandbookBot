using HandbookBot.Core.Models;
using System.ComponentModel.DataAnnotations.Schema;

namespace HandbookBot.Core.Entities;

[Table("preparations")]
public partial class PreparationEntity
{
    [Column("id")]
    public int Id { get; init; }
    [Column("name")]
    public string Name { get; init; } = string.Empty;
    [Column("description")]
    public string Description { get; init; } = string.Empty;
    [Column("price")]
    public decimal Price { get; init; }
    [Column("pharmacy_id")]
    public int PharmacyId { get; init; }
    [Column("is_available")]
    public bool IsAvailable { get; init; }

    public Preparation ToDomain() => new(Id, Name, Price, PharmacyId, IsAvailable, Description);
}
