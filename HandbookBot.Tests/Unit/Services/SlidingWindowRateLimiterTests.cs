using HandbookBot.Core.Services;

namespace HandbookBot.Tests.Unit.Services;

public class SlidingWindowRateLimiterTests
{
    private sealed class TestTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow;

        public TestTimeProvider(DateTimeOffset initial) => _utcNow = initial;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan delta) => _utcNow = _utcNow.Add(delta);
    }

    [Fact]
    public void IsAllowed_WithinLimit_ReturnsTrue()
    {
        // Arrange
        var time = new TestTimeProvider(DateTimeOffset.UtcNow);
        var sut = new SlidingWindowRateLimiter(maxRequests: 3, window: TimeSpan.FromSeconds(5), timeProvider: time);

        // Act & Assert
        Assert.True(sut.IsAllowed("user1"));
        Assert.True(sut.IsAllowed("user1"));
        Assert.True(sut.IsAllowed("user1"));
    }

    [Fact]
    public void IsAllowed_ExceedsLimit_ReturnsFalse()
    {
        // Arrange
        var time = new TestTimeProvider(DateTimeOffset.UtcNow);
        var sut = new SlidingWindowRateLimiter(maxRequests: 2, window: TimeSpan.FromSeconds(5), timeProvider: time);

        // Act & Assert
        Assert.True(sut.IsAllowed("user1"));
        Assert.True(sut.IsAllowed("user1"));
        Assert.False(sut.IsAllowed("user1")); // 3rd request in window
    }

    [Fact]
    public void IsAllowed_AfterWindowExpires_ResetsAndAllows()
    {
        // Arrange
        var time = new TestTimeProvider(DateTimeOffset.UtcNow);
        var sut = new SlidingWindowRateLimiter(maxRequests: 2, window: TimeSpan.FromSeconds(5), timeProvider: time);

        // Act & Assert
        Assert.True(sut.IsAllowed("user1"));
        Assert.True(sut.IsAllowed("user1"));
        Assert.False(sut.IsAllowed("user1"));

        // Advance time beyond window
        time.Advance(TimeSpan.FromSeconds(6));

        Assert.True(sut.IsAllowed("user1"));
    }

    [Fact]
    public void IsAllowed_DifferentKeys_AreIsolated()
    {
        // Arrange
        var time = new TestTimeProvider(DateTimeOffset.UtcNow);
        var sut = new SlidingWindowRateLimiter(maxRequests: 1, window: TimeSpan.FromSeconds(5), timeProvider: time);

        // Act & Assert
        Assert.True(sut.IsAllowed("telegram:100"));
        Assert.False(sut.IsAllowed("telegram:100"));

        // Different user should still be allowed
        Assert.True(sut.IsAllowed("max:100"));
    }
}
