using HandbookBot.Core.Commands;
using HandbookBot.Core.Models;
using HandbookBot.Tests.Helpers;
using NSubstitute;

namespace HandbookBot.Tests.Unit.Commands;

public class ContactsCommandTests
{
    private readonly ContactsCommand _sut;
    private readonly FakeMessagingPlatform _platform;
    private readonly BotContext _context;

    public ContactsCommandTests()
    {
        _sut = new ContactsCommand();
        _platform = new FakeMessagingPlatform();
        var sessions = Substitute.For<HandbookBot.Core.Interfaces.IUserSessionStore>();
        _context = new BotContext("chat", "user", "Test", _platform, sessions);
    }

    [Fact]
    public async Task ExecuteAsync_SendsContactsInfo()
    {
        // Arrange
        var msg = new IncomingMessage("chat", "user", "", "contacts:show", "Test");

        // Act
        await _sut.ExecuteAsync(_context, msg);

        // Assert
        var sent = Assert.Single(_platform.SentMessages);
        Assert.Contains("Контакты", sent.Text);
        Assert.Contains("460050", sent.Text); // index check
        Assert.NotNull(sent.Keyboard);
        Assert.Equal("start:menu", sent.Keyboard.Rows[0][0].Payload);
    }
}
