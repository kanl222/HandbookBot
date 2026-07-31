using HandbookBot.Data.EfCore;
using HandbookBot.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace HandbookBot.Tests.Integration;

public class EfFaqRepositoryTests : IDisposable
{
    private readonly BotDbContext _db;
    private readonly EfFaqRepository _sut;

    public EfFaqRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<BotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new BotDbContext(options);
        _db.Database.EnsureCreated();

        _sut = new EfFaqRepository(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    [Fact]
    public async Task GetAllAsync_ReturnsOrderedEntries()
    {
        // Arrange
        _db.FaqEntries.AddRange(
            TestData.CreateFaqEntryEntity(3, "Q3", "A3"),
            TestData.CreateFaqEntryEntity(1, "Q1", "A1"),
            TestData.CreateFaqEntryEntity(2, "Q2", "A2")
        );
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetAllAsync();

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal(1, result[0].Id);
        Assert.Equal(2, result[1].Id);
        Assert.Equal(3, result[2].Id);
    }
}
