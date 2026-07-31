using HandbookBot.Core.Commands;
using HandbookBot.Core.Interfaces;
using HandbookBot.Core.Models;
using HandbookBot.Tests.Helpers;
using NSubstitute;

namespace HandbookBot.Tests.Unit.Commands;

public class PreparationSearchCommandTests
{
    private readonly IPreparationRepository _repo;
    private readonly PreparationSearchCommand _sut;
    private readonly FakeMessagingPlatform _platform;
    private readonly IUserSessionStore _sessions;
    private readonly BotContext _context;

    public PreparationSearchCommandTests()
    {
        _repo = Substitute.For<IPreparationRepository>();
        _sut = new PreparationSearchCommand(_repo);
        _platform = new FakeMessagingPlatform();
        _sessions = Substitute.For<IUserSessionStore>();
        _context = new BotContext("chat", "user", "Test", _platform, _sessions);
    }

    [Fact]
    public async Task ExecuteAsync_BeginCallback_SetsSession()
    {
        // Arrange
        var msg = new IncomingMessage("chat", "user", "", "prepsearch:begin", "Test");

        // Act
        await _sut.ExecuteAsync(_context, msg);

        // Assert
        await _sessions.Received(1).SetStateAsync(
            "user", 
            Arg.Is<UserDialogState>(s => s.AwaitingInputFor == "prepsearch"),
            Arg.Any<TimeSpan?>(),
            Arg.Any<CancellationToken>());

        var sent = Assert.Single(_platform.SentMessages);
        Assert.Contains("Введите название", sent.Text);
        Assert.NotNull(sent.Keyboard); // Cancel button
    }

    [Fact]
    public async Task ExecuteAsync_EmptySearchText_ShowsError()
    {
        // Arrange
        var msg = new IncomingMessage("chat", "user", "   ", null, "Test");

        // Act
        await _sut.ExecuteAsync(_context, msg);

        // Assert
        await _sessions.Received(1).ClearStateAsync("user", Arg.Any<CancellationToken>());
        var sent = Assert.Single(_platform.SentMessages);
        Assert.Contains("не может быть пустым", sent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_ValidText_ShowsResults()
    {
        // Arrange
        var msg = new IncomingMessage("chat", "user", "Aspirin", null, "Test");
        var items = new[] { TestData.CreatePreparation(1, "Aspirin C", 100m, 1) };
        _repo.SearchAsync("Aspirin", 1, 5, Arg.Any<CancellationToken>())
            .Returns(new PagedResult<Preparation>(items, 1, 1, 5));

        // Act
        await _sut.ExecuteAsync(_context, msg);

        // Assert
        var sent = Assert.Single(_platform.SentMessages);
        Assert.Contains("Результаты поиска «Aspirin»", sent.Text);
        Assert.Contains("Aspirin C", sent.Text);
    }
}
