using HandbookBot.Core.Commands;
using HandbookBot.Core.Models;
using HandbookBot.Tests.Helpers;
using NSubstitute;

namespace HandbookBot.Tests.Unit.Commands;

public class InstructionCommandTests
{
    private readonly InstructionCommand _sut;
    private readonly FakeMessagingPlatform _platform;
    private readonly BotContext _context;

    public InstructionCommandTests()
    {
        _sut = new InstructionCommand();
        _platform = new FakeMessagingPlatform();
        var sessions = Substitute.For<IUserSessionStore>();
        _context = new BotContext("chat", "user", "Test", _platform, sessions);
    }

    [Fact]
    public async Task ExecuteAsync_SendsInstructionInfo()
    {
        // Arrange
        var msg = new IncomingMessage("chat", "user", "", "instruction:show", "Test");

        // Act
        await _sut.ExecuteAsync(_context, msg);

        // Assert
        var sent = Assert.Single(_platform.SentMessages);
        Assert.Contains("Инструкция по использованию чат-бота", sent.Text);
        Assert.NotNull(sent.Keyboard);
        Assert.Equal("start:menu", sent.Keyboard.Rows[0][0].Payload);
    }
}
