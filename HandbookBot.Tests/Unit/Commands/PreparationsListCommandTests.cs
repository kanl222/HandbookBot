using HandbookBot.Core.Commands;
using HandbookBot.Core.Interfaces;
using HandbookBot.Core.Models;
using HandbookBot.Tests.Helpers;
using NSubstitute;

namespace HandbookBot.Tests.Unit.Commands;

public class PreparationsListCommandTests
{
    private readonly IPreparationRepository _repo;
    private readonly PreparationsListCommand _sut;
    private readonly FakeMessagingPlatform _platform;
    private readonly BotContext _context;

    public PreparationsListCommandTests()
    {
        _repo = Substitute.For<IPreparationRepository>();
        _sut = new PreparationsListCommand(_repo);
        _platform = new FakeMessagingPlatform();
        var sessions = Substitute.For<IUserSessionStore>();
        _context = new BotContext("chat", "user", "Test", _platform, sessions);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyList_SendsEmptyMessage()
    {
        // Arrange
        _repo.GetPageAsync(1, 5, Arg.Any<CancellationToken>())
            .Returns(new PagedResult<Preparation>(Array.Empty<Preparation>(), 0, 1, 5));
        var msg = new IncomingMessage("chat", "user", "", null, "Test");

        // Act
        await _sut.ExecuteAsync(_context, msg);

        // Assert
        var sent = Assert.Single(_platform.SentMessages);
        Assert.Equal("Список препаратов пуст.", sent.Text);
        Assert.Null(sent.Keyboard);
    }

    [Fact]
    public async Task ExecuteAsync_WithItems_SendsListAndNavButtons()
    {
        // Arrange
        var items = new[]
        {
            TestData.CreatePreparation(1, "Aspirin", 10.5m, 1, true),
            TestData.CreatePreparation(2, "Nurofen", 25.0m, 2, false)
        };
        _repo.GetPageAsync(2, 5, Arg.Any<CancellationToken>())
            .Returns(new PagedResult<Preparation>(items, 15, 2, 5)); // TotalPages = 3

        var msg = new IncomingMessage("chat", "user", "", "preparations:2", "Test");

        // Act
        await _sut.ExecuteAsync(_context, msg);

        // Assert
        var sent = Assert.Single(_platform.SentMessages);
        Assert.Contains("Препараты", sent.Text);
        Assert.Contains("стр. 2/3", sent.Text);
        Assert.Contains("1. *Aspirin*", sent.Text);
        Assert.Contains("В наличии", sent.Text);
        Assert.Contains("2. *Nurofen*", sent.Text);
        Assert.Contains("Нет в наличии", sent.Text);

        Assert.NotNull(sent.Keyboard);
        // Buttons: 1 map buttons row ( 1,  2), 1 nav row (Назад, Вперёд), 1 search button, 1 menu button -> 4 rows total
        Assert.Equal(4, sent.Keyboard.Rows.Count);

        var mapRow = sent.Keyboard.Rows[0];
        Assert.Equal(2, mapRow.Count);
        Assert.Equal("1", mapRow[0].Text);
        Assert.Equal("pharmmap:1", mapRow[0].Payload);
        Assert.Equal("2", mapRow[1].Text);
        Assert.Equal("pharmmap:2", mapRow[1].Payload);
        
        var navRow = sent.Keyboard.Rows[1];
        Assert.Equal(2, navRow.Count); // Back, Forward
        Assert.Equal("preparations:1", navRow[0].Payload);
        Assert.Equal("preparations:3", navRow[1].Payload);
    }
}
