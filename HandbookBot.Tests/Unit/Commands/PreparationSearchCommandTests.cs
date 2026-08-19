using HandbookBot.Core.Commands;
using HandbookBot.Core.Interfaces;
using HandbookBot.Core.Models;
using HandbookBot.Tests.Helpers;
using NSubstitute;

namespace HandbookBot.Tests.Unit.Commands;

public class PreparationSearchCommandTests
{
    private readonly IPreparationRepository _repo;
    private readonly IPharmacyRepository _pharmacyRepo;
    private readonly PreparationSearchCommand _sut;
    private readonly FakeMessagingPlatform _platform;
    private readonly IUserSessionStore _sessions;
    private readonly BotContext _context;

    public PreparationSearchCommandTests()
    {
        _repo = Substitute.For<IPreparationRepository>();
        _pharmacyRepo = Substitute.For<IPharmacyRepository>();
        _sut = new PreparationSearchCommand(_repo, _pharmacyRepo);
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
            "test:user", 
            Arg.Is<UserDialogState>(s => s != null && s.AwaitingInputFor == "prepsearch"),
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
        var sent = Assert.Single(_platform.SentMessages);
        Assert.Contains("не может быть пустым", sent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_ValidText_ShowsResultsWithPharmacy()
    {
        // Arrange
        var msg = new IncomingMessage("chat", "user", "Aspirin", null, "Test");
        var items = new[]
        {
            TestData.CreatePreparation(1, "Aspirin C", 100m, 1),
            TestData.CreatePreparation(2, "Aspirin Cardio", 200m, 2)
        };
        _repo.SearchAsync("Aspirin", 1, 5, Arg.Any<CancellationToken>())
            .Returns(new PagedResult<Preparation>(items, 2, 1, 5));
        _pharmacyRepo.GetByIdAsync(1, Arg.Any<CancellationToken>())
            .Returns(TestData.CreatePharmacy(1, "Аптека №1", "ул. Ленина, 1"));
        _pharmacyRepo.GetByIdAsync(2, Arg.Any<CancellationToken>())
            .Returns(TestData.CreatePharmacy(2, "Аптека №2", "пр. Маркса, 34"));

        // Act
        await _sut.ExecuteAsync(_context, msg);

        // Assert
        var sent = Assert.Single(_platform.SentMessages);
        Assert.Contains("Результаты поиска «Aspirin»", sent.Text);
        Assert.Contains("1. *Aspirin C*", sent.Text);
        Assert.Contains("Аптека: Аптека №1", sent.Text);
        Assert.Contains("Адрес: ул. Ленина, 1", sent.Text);
        Assert.Contains("2. *Aspirin Cardio*", sent.Text);
        Assert.Contains("Аптека: Аптека №2", sent.Text);
        Assert.Contains("Адрес: пр. Маркса, 34", sent.Text);

        Assert.NotNull(sent.Keyboard);
        var prepRow = sent.Keyboard.Rows[0];
        Assert.Equal(2, prepRow.Count);
        Assert.Equal("1", prepRow[0].Text);
        Assert.Equal("prepinfo:1", prepRow[0].Payload);
        Assert.Equal("2", prepRow[1].Text);
        Assert.Equal("prepinfo:2", prepRow[1].Payload);
    }

    [Fact]
    public async Task ExecuteAsync_PaginationCallback_ReadsQueryFromSessionAndShowsResults()
    {
        // Arrange
        var msg = new IncomingMessage("chat", "user", "", "prepsearch:2", "Test");
        _sessions.GetStateAsync("test:user", Arg.Any<CancellationToken>())
            .Returns(new UserDialogState("prepsearch") { SearchQuery = "Aspirin" });

        var items = new[] { TestData.CreatePreparation(1, "Aspirin C", 100m, 1) };
        _repo.SearchAsync("Aspirin", 2, 5, Arg.Any<CancellationToken>())
            .Returns(new PagedResult<Preparation>(items, 6, 2, 5));
        _pharmacyRepo.GetByIdAsync(1, Arg.Any<CancellationToken>())
            .Returns(TestData.CreatePharmacy(1, "Аптека №1", "ул. Ленина, 1"));

        // Act
        await _sut.ExecuteAsync(_context, msg);

        // Assert
        var sent = Assert.Single(_platform.SentMessages);
        Assert.Contains("Результаты поиска «Aspirin»", sent.Text);
        Assert.Contains("стр. 2/2", sent.Text);
        Assert.Contains("Аптека: Аптека №1", sent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_PaginationCallbackExpiredSession_ShowsError()
    {
        // Arrange
        var msg = new IncomingMessage("chat", "user", "", "prepsearch:2", "Test");
        _sessions.GetStateAsync("test:user", Arg.Any<CancellationToken>())
            .Returns((UserDialogState?)null); // Expired session

        // Act
        await _sut.ExecuteAsync(_context, msg);

        // Assert
        var sent = Assert.Single(_platform.SentMessages);
        Assert.Contains("Время сессии поиска истекло", sent.Text);
    }
}
