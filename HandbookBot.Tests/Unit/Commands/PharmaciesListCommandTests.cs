using HandbookBot.Core.Commands;
using HandbookBot.Core.Interfaces;
using HandbookBot.Core.Models;
using HandbookBot.Tests.Helpers;
using NSubstitute;

namespace HandbookBot.Tests.Unit.Commands;

public class PharmaciesListCommandTests
{
    private readonly IPharmacyRepository _repo;
    private readonly PharmaciesListCommand _sut;
    private readonly FakeMessagingPlatform _platform;
    private readonly BotContext _context;

    public PharmaciesListCommandTests()
    {
        _repo = Substitute.For<IPharmacyRepository>();
        _sut = new PharmaciesListCommand(_repo);
        _platform = new FakeMessagingPlatform();
        var sessions = Substitute.For<IUserSessionStore>();
        _context = new BotContext("chat", "user", "Test", _platform, sessions);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyList_SendsEmptyMessage()
    {
        // Arrange
        _repo.GetPageAsync(1, 3, Arg.Any<CancellationToken>())
            .Returns(new PagedResult<Pharmacy>(Array.Empty<Pharmacy>(), 0, 1, 3));
        var msg = new IncomingMessage("chat", "user", "", null, "Test");

        // Act
        await _sut.ExecuteAsync(_context, msg);

        // Assert
        var sent = Assert.Single(_platform.SentMessages);
        Assert.Equal("Список аптечных пунктов пуст.", sent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_WithItems_SendsList()
    {
        // Arrange
        var items = new[] { TestData.CreatePharmacy(1, "Pharma 1", "Address 1") };
        _repo.GetPageAsync(1, 3, Arg.Any<CancellationToken>())
            .Returns(new PagedResult<Pharmacy>(items, 1, 1, 3));
        var msg = new IncomingMessage("chat", "user", "", "pharmacies:1", "Test");

        // Act
        await _sut.ExecuteAsync(_context, msg);

        // Assert
        var sent = Assert.Single(_platform.SentMessages);
        Assert.Contains("Аптечные пункты", sent.Text);
        Assert.Contains("Pharma 1", sent.Text);
        Assert.Contains("Address 1", sent.Text);
    }
}
