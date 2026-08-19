using HandbookBot.Core.Entities;
using HandbookBot.Core.Models;

namespace HandbookBot.Tests.Helpers;

public static class TestData
{
    public static Preparation CreatePreparation(int id, string name, decimal price, int pharmacyId, bool isAvailable = true, string description = "")
        => new(id, name, price, pharmacyId, isAvailable, description);

    public static Pharmacy CreatePharmacy(int id, string name, string address, double lat = 0, double lon = 0, string contact = "")
        => new(id, name, address, lat, lon, contact);

    public static FaqEntry CreateFaqEntry(int id, string question, string answer)
        => new(id, question, answer);

    public static PreparationEntity CreatePreparationEntity(int id, string name, decimal price, int pharmacyId, bool isAvailable = true, string description = "")
        => new() { Id = id, Name = name, Price = price, PharmacyId = pharmacyId, IsAvailable = isAvailable, Description = description };

    public static PharmacyEntity CreatePharmacyEntity(int id, string name, string address, double lat = 0, double lon = 0, string contact = "")
        => new() { Id = id, Name = name, Address = address, Latitude = lat, Longitude = lon, Contact = contact };

    public static FaqEntryEntity CreateFaqEntryEntity(int id, string question, string answer)
        => new() { Id = id, Question = question, Answer = answer };
}
