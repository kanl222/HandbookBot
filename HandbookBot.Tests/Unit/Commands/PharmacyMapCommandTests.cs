using HandbookBot.Core.Commands;
using HandbookBot.Core.Interfaces;
using HandbookBot.Core.Models;
using HandbookBot.Tests.Helpers;
using NSubstitute;

namespace HandbookBot.Tests.Unit.Commands;

public class PharmacyMapCommandTests
{
    private readonly IPharmacyRepository _repo;
    private readonly PharmacyMapCommand _sut;
    private readonly FakeMessagingPlatform _platform;
    private readonly BotContext _context;

    public PharmacyMapCommandTests()
    {
        _repo = Substitute.For<IPharmacyRepository>();
        _sut = new PharmacyMapCommand(_repo);
        _platform = new FakeMessagingPlatform();
        var sessions = Substitute.For<IUserSessionStore>();
        _context = new BotContext("chat", "user", "Test", _platform, sessions);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidId_SendsError()
    {
        // Arrange
        var msg = new IncomingMessage("chat", "user", "", "pharmmap:abc", "Test");

        // Act
        await _sut.ExecuteAsync(_context, msg);

        // Assert
        var sent = Assert.Single(_platform.SentMessages);
        Assert.Contains("Некорректный идентификатор", sent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_NotFound_SendsError()
    {
        // Arrange
        _repo.GetByIdAsync(99, Arg.Any<CancellationToken>()).Returns((Pharmacy?)null);
        var msg = new IncomingMessage("chat", "user", "", "pharmmap:99", "Test");

        // Act
        await _sut.ExecuteAsync(_context, msg);

        // Assert
        var sent = Assert.Single(_platform.SentMessages);
        Assert.Contains("Аптека не найдена", sent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_Found_SendsLocation()
    {
        // Arrange
        var item = TestData.CreatePharmacy(1, "P1", "A1", 55.5, 37.7);
        _repo.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(item);
        var msg = new IncomingMessage("chat", "user", "", "pharmmap:1", "Test");

        // Act
        await _sut.ExecuteAsync(_context, msg);

        // Assert
        var sentMsg = Assert.Single(_platform.SentMessages);
        Assert.Contains("P1", sentMsg.Text);

        var sentLoc = Assert.Single(_platform.SentLocations);
        Assert.Equal(55.5, sentLoc.Latitude);
        Assert.Equal(37.7, sentLoc.Longitude);
    }
}
