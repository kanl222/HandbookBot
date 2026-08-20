using HandbookBot.Core.Models;
using HandbookBot.Data.Caching;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace HandbookBot.Tests.Unit.Services;

public class DistributedCacheUserSessionStoreTests
{
    private readonly DistributedCacheUserSessionStore _sut;
    private readonly IDistributedCache _cache;

    public DistributedCacheUserSessionStoreTests()
    {
        var opts = Options.Create(new MemoryDistributedCacheOptions());
        _cache = new MemoryDistributedCache(opts);
        _sut = new DistributedCacheUserSessionStore(_cache);
    }

    [Fact]
    public async Task GetStateAsync_NotSet_ReturnsNull()
    {
        // Act
        var state = await _sut.GetStateAsync("user1");

        // Assert
        Assert.Null(state);
    }

    [Fact]
    public async Task SetStateAsync_AndGet_ReturnsState()
    {
        // Arrange
        var expected = new UserDialogState("test_cmd")
        {
            SearchQuery = "Aspirin"
        };

        // Act
        await _sut.SetStateAsync("user1", expected);
        var actual = await _sut.GetStateAsync("user1");

        // Assert
        Assert.NotNull(actual);
        Assert.Equal("test_cmd", actual!.Value.AwaitingInputFor);
        Assert.Equal("Aspirin", actual!.Value.SearchQuery);
    }

    [Fact]
    public async Task ClearStateAsync_RemovesState()
    {
        // Arrange
        await _sut.SetStateAsync("user1", new UserDialogState("test_cmd"));

        // Act
        await _sut.ClearStateAsync("user1");
        var actual = await _sut.GetStateAsync("user1");

        // Assert
        Assert.Null(actual);
    }
}
