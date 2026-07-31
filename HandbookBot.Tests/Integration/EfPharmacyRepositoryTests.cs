using HandbookBot.Data.EfCore;
using HandbookBot.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace HandbookBot.Tests.Integration;

public class EfPharmacyRepositoryTests : IDisposable
{
    private readonly BotDbContext _db;
    private readonly EfPharmacyRepository _sut;

    public EfPharmacyRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<BotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new BotDbContext(options);
        _db.Database.EnsureCreated();

        _sut = new EfPharmacyRepository(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    [Fact]
    public async Task GetPageAsync_ReturnsOrderedPage()
    {
        // Arrange
        _db.Pharmacies.AddRange(
            TestData.CreatePharmacyEntity(1, "Z Pharmacy", "Addr 1"),
            TestData.CreatePharmacyEntity(2, "A Pharmacy", "Addr 2"),
            TestData.CreatePharmacyEntity(3, "M Pharmacy", "Addr 3")
        );
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetPageAsync(1, 2);

        // Assert
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal("A Pharmacy", result.Items[0].Name); // Ordered by Name
        Assert.Equal("M Pharmacy", result.Items[1].Name);
    }

    [Fact]
    public async Task GetByIdAsync_Existing_ReturnsPharmacy()
    {
        // Arrange
        _db.Pharmacies.Add(TestData.CreatePharmacyEntity(1, "A Pharmacy", "Addr 2"));
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetByIdAsync(1);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.Id);
    }

    [Fact]
    public async Task GetByIdAsync_NotExisting_ReturnsNull()
    {
        // Act
        var result = await _sut.GetByIdAsync(99);

        // Assert
        Assert.Null(result);
    }
}
