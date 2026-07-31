using HandbookBot.Core.Models;
using System.ComponentModel.DataAnnotations.Schema;

namespace HandbookBot.Core.Entities;

[Table("pharmacies")]
public partial class PharmacyEntity
{
    [Column("id")]
    public int Id { get; init; }
    [Column("name")]
    public string Name { get; init; } = string.Empty;
    [Column("address")]
    public string Address { get; init; } = string.Empty;
    [Column("latitude")]
    public double Latitude { get; init; }
    [Column("longitude")]
    public double Longitude { get; init; }
    [Column("contact")]
    public string Contact { get; init; } = string.Empty;

    public Pharmacy ToDomain() => new(Id, Name, Address, Latitude, Longitude, Contact);
}
