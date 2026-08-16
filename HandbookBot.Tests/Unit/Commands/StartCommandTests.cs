using HandbookBot.Core.Commands;
using HandbookBot.Core.Interfaces;
using HandbookBot.Core.Models;
using HandbookBot.Tests.Helpers;
using NSubstitute;

namespace HandbookBot.Tests.Unit.Commands;

public class StartCommandTests
{
    private readonly StartCommand _sut;
    private readonly FakeMessagingPlatform _platform;
    private readonly IUserSessionStore _sessions;

    public StartCommandTests()
    {
        _sut = new StartCommand();
        _platform = new FakeMessagingPlatform();
        _sessions = Substitute.For<IUserSessionStore>();
    }

    [Fact]
    public async Task ExecuteAsync_SendsWelcomeMessageAndClearsSession()
    {
        // Arrange
        var context = new BotContext("chat-1", "user-1", "Telegram", _platform, _sessions);
        var message = new IncomingMessage("chat-1", "user-1", "/start", null, "Telegram");

        // Act
        await _sut.ExecuteAsync(context, message);

        // Assert
        await _sessions.Received(1).ClearStateAsync("telegram:user-1", Arg.Any<CancellationToken>());

        var sent = Assert.Single(_platform.SentMessages);
        Assert.Equal("chat-1", sent.ChatId);
        Assert.Contains("Добро пожаловать в справочник", sent.Text);
        Assert.NotNull(sent.Keyboard);
        Assert.Equal(6, sent.Keyboard.Rows.Sum(r => r.Count)); // 6 buttons
    }
}
