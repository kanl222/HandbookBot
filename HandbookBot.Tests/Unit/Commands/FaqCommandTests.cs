using HandbookBot.Core.Commands;
using HandbookBot.Core.Interfaces;
using HandbookBot.Core.Models;
using HandbookBot.Tests.Helpers;
using NSubstitute;

namespace HandbookBot.Tests.Unit.Commands;

public class FaqCommandTests
{
    private readonly IFaqRepository _repo;
    private readonly FaqCommand _sut;
    private readonly FakeMessagingPlatform _platform;
    private readonly BotContext _context;

    public FaqCommandTests()
    {
        _repo = Substitute.For<IFaqRepository>();
        _sut = new FaqCommand(_repo);
        _platform = new FakeMessagingPlatform();
        var sessions = Substitute.For<IUserSessionStore>();
        _context = new BotContext("chat", "user", "Test", _platform, sessions);
    }

    [Fact]
    public async Task ExecuteAsync_ListAction_SendsList()
    {
        // Arrange
        var items = new[] { TestData.CreateFaqEntry(1, "Q1", "A1") };
        _repo.GetAllAsync(Arg.Any<CancellationToken>()).Returns(items);
        var msg = new IncomingMessage("chat", "user", "", "faq:list", "Test");

        // Act
        await _sut.ExecuteAsync(_context, msg);

        // Assert
        var sent = Assert.Single(_platform.SentMessages);
        Assert.Contains("Часто задаваемые вопросы", sent.Text);
        Assert.NotNull(sent.Keyboard);
        Assert.Equal(2, sent.Keyboard.Rows.Count); // 1 question + 1 menu button
        Assert.Equal("faq:1", sent.Keyboard.Rows[0][0].Payload);
    }

    [Fact]
    public async Task ExecuteAsync_QuestionAction_SendsAnswer()
    {
        // Arrange
        var items = new[] { TestData.CreateFaqEntry(42, "Q42", "A42") };
        _repo.GetAllAsync(Arg.Any<CancellationToken>()).Returns(items);
        var msg = new IncomingMessage("chat", "user", "", "faq:42", "Test");

        // Act
        await _sut.ExecuteAsync(_context, msg);

        // Assert
        var sent = Assert.Single(_platform.SentMessages);
        Assert.Contains("Q42", sent.Text);
        Assert.Contains("A42", sent.Text);
        Assert.NotNull(sent.Keyboard);
        Assert.Equal("faq:list", sent.Keyboard.Rows[0][0].Payload);
    }
}
