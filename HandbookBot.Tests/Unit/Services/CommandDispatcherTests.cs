using HandbookBot.Core.Commands;
using HandbookBot.Core.Interfaces;
using HandbookBot.Core.Models;
using HandbookBot.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace HandbookBot.Tests.Unit.Services;

public class CommandDispatcherTests
{
    private readonly ICommandFactory _factory;
    private readonly IUserSessionStore _sessions;
    private readonly IRateLimiter _rateLimiter;
    private readonly CommandDispatcher _sut;
    private readonly FakeMessagingPlatform _platform;

    public CommandDispatcherTests()
    {
        _factory = Substitute.For<ICommandFactory>();
        _sessions = Substitute.For<IUserSessionStore>();
        _rateLimiter = Substitute.For<IRateLimiter>();
        _rateLimiter.IsAllowed(Arg.Any<string>()).Returns(true);
        _platform = new FakeMessagingPlatform();
        
        _sut = new CommandDispatcher(_factory, _sessions, _rateLimiter, NullLogger<CommandDispatcher>.Instance);
    }

    [Fact]
    public async Task DispatchAsync_WithCallback_ResolvesCommand()
    {
        // Arrange
        var command = Substitute.For<IBotCommand>();
        _factory.Resolve("testcmd").Returns(command);
        var msg = new IncomingMessage("chat", "user", "", "testcmd:123", "Test");

        // Act
        await _sut.DispatchAsync(msg, _platform);

        // Assert
        await command.Received(1).ExecuteAsync(Arg.Any<BotContext>(), msg, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchAsync_WithUnknownCallback_SendsError()
    {
        // Arrange
        _factory.Resolve("unknown").Returns((IBotCommand?)null);
        var msg = new IncomingMessage("chat", "user", "", "unknown:123", "Test");

        // Act
        await _sut.DispatchAsync(msg, _platform);

        // Assert
        var sent = Assert.Single(_platform.SentMessages);
        Assert.Contains("Неизвестная кнопка", sent.Text);
    }

    [Fact]
    public async Task DispatchAsync_WithActiveSession_ResolvesSessionCommand()
    {
        // Arrange
        _sessions.GetStateAsync("test:user", Arg.Any<CancellationToken>())
            .Returns(new UserDialogState("sessioncmd"));
        var command = Substitute.For<IBotCommand>();
        _factory.Resolve("sessioncmd").Returns(command);
        var msg = new IncomingMessage("chat", "user", "some input", null, "Test");

        // Act
        await _sut.DispatchAsync(msg, _platform);

        // Assert
        await command.Received(1).ExecuteAsync(Arg.Any<BotContext>(), msg, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchAsync_WithTextCommand_ResolvesCommand()
    {
        // Arrange
        _sessions.GetStateAsync("test:user", Arg.Any<CancellationToken>()).Returns((UserDialogState?)null);
        var command = Substitute.For<IBotCommand>();
        _factory.Resolve("start").Returns(command);
        var msg = new IncomingMessage("chat", "user", "/start", null, "Test");

        // Act
        await _sut.DispatchAsync(msg, _platform);

        // Assert
        await command.Received(1).ExecuteAsync(Arg.Any<BotContext>(), msg, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchAsync_WhenRateLimited_SendsWarningMessage()
    {
        // Arrange
        _rateLimiter.IsAllowed(Arg.Any<string>()).Returns(false);
        var msg = new IncomingMessage("chat", "user", "/start", null, "Test");

        // Act
        await _sut.DispatchAsync(msg, _platform);

        // Assert
        var sent = Assert.Single(_platform.SentMessages);
        Assert.Contains("Слишком много запросов", sent.Text);
    }
}
