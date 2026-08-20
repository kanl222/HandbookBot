using BotEngine.Core.Models;
using BotEngine.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace HandbookBot.Tests.Unit.Services;

public class InMemoryUserSessionStoreTests
{
    private readonly InMemoryUserSessionStore _sut;

    public InMemoryUserSessionStoreTests()
    {
        _sut = new InMemoryUserSessionStore(NullLogger<InMemoryUserSessionStore>.Instance);
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
        var expected = new UserDialogState("test_cmd");

        // Act
        await _sut.SetStateAsync("user1", expected);
        var actual = await _sut.GetStateAsync("user1");

        // Assert
        Assert.NotNull(actual);
        Assert.Equal("test_cmd", actual!.Value.AwaitingInputFor);
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

    [Fact]
    public async Task GetStateAsync_ExpiredTtl_ReturnsNull()
    {
        // Arrange
        await _sut.SetStateAsync("user1", new UserDialogState("test_cmd"), TimeSpan.FromMilliseconds(1));
        await Task.Delay(10); // Wait for expiration

        // Act
        var actual = await _sut.GetStateAsync("user1");

        // Assert
        Assert.Null(actual);
    }
}
