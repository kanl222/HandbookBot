using HandbookBot.Data.EfCore;
using HandbookBot.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace HandbookBot.Tests.Integration;

public class EfPreparationRepositoryTests : IDisposable
{
    private readonly BotDbContext _db;
    private readonly EfPreparationRepository _sut;

    public EfPreparationRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<BotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new BotDbContext(options);
        _db.Database.EnsureCreated();

        _sut = new EfPreparationRepository(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    [Fact]
    public async Task GetPageAsync_ReturnsOrderedPageByPriceDesc()
    {
        // Arrange
        _db.Preparations.AddRange(
            TestData.CreatePreparationEntity(1, "Prep 1", 10.0m, 1),
            TestData.CreatePreparationEntity(2, "Prep 2", 30.0m, 1),
            TestData.CreatePreparationEntity(3, "Prep 3", 20.0m, 1)
        );
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetPageAsync(1, 2);

        // Assert
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
        // Ordered descending by price
        Assert.Equal("Prep 2", result.Items[0].Name); // 30.0
        Assert.Equal("Prep 3", result.Items[1].Name); // 20.0
    }

    [Fact]
    public async Task SearchAsync_FiltersByNameLike()
    {
        // Arrange
        _db.Preparations.AddRange(
            TestData.CreatePreparationEntity(1, "Aspirin C", 10.0m, 1),
            TestData.CreatePreparationEntity(2, "Nurofen", 30.0m, 1),
            TestData.CreatePreparationEntity(3, "Aspirin Cardio", 20.0m, 1)
        );
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.SearchAsync("aspirin", 1, 10);

        // Assert
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
        Assert.Contains(result.Items, p => p.Name == "Aspirin C");
        Assert.Contains(result.Items, p => p.Name == "Aspirin Cardio");
    }

    [Fact]
    public async Task SearchAsync_EscapesSpecialCharacters()
    {
        // Arrange
        _db.Preparations.AddRange(
            TestData.CreatePreparationEntity(1, "Prep%test", 10.0m, 1),
            TestData.CreatePreparationEntity(2, "Prep_test", 30.0m, 1),
            TestData.CreatePreparationEntity(3, "Preptest", 20.0m, 1)
        );
        await _db.SaveChangesAsync();

        // Act
        var result1 = await _sut.SearchAsync("%", 1, 10);
        var result2 = await _sut.SearchAsync("_", 1, 10);

        // Assert
        Assert.Equal(1, result1.TotalCount);
        Assert.Equal("Prep%test", result1.Items[0].Name);

        Assert.Equal(1, result2.TotalCount);
        Assert.Equal("Prep_test", result2.Items[0].Name);
    }
}
