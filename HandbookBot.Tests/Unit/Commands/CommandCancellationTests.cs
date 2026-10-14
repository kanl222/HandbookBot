using HandbookBot.Core.Commands;
using HandbookBot.Core.Interfaces;
using HandbookBot.Core.Models;
using HandbookBot.Tests.Helpers;
using NSubstitute;

namespace HandbookBot.Tests.Unit.Commands;

public class CommandCancellationTests
{
    [Theory]
    [InlineData("start")]
    [InlineData("contacts")]
    [InlineData("instruction")]
    [InlineData("faq")]
    [InlineData("preparations")]
    [InlineData("pharmacies")]
    [InlineData("prepsearch")]
    [InlineData("prepinfo")]
    [InlineData("pharmmap")]
    [InlineData("location")]
    public async Task OutgoingOperation_IsCancelledWithTheCommand(string name)
    {
        using var cancellation = new CancellationTokenSource();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task Send(CancellationToken token)
        {
            token.Register(() => finished.TrySetCanceled(token));
            started.TrySetResult();
            return finished.Task;
        }
        var platform = Substitute.For<IMessagingPlatform>();
        platform.SendTextAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<BotKeyboard?>(), Arg.Any<CancellationToken>())
            .Returns(call => Send(call.Arg<CancellationToken>()));
        platform.SendLocationAsync(Arg.Any<string>(), Arg.Any<double>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .Returns(call => Send(call.Arg<CancellationToken>()));

        var preparations = Substitute.For<IPreparationRepository>();
        preparations.GetPageAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<Preparation>(Array.Empty<Preparation>(), 0, 1, 5));
        var pharmacies = Substitute.For<IPharmacyRepository>();
        pharmacies.GetPageAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<Pharmacy>(Array.Empty<Pharmacy>(), 0, 1, 3));
        pharmacies.GetByIdAsync(1, Arg.Any<CancellationToken>())
            .Returns(TestData.CreatePharmacy(1, "Pharmacy", "Address", 55.5, 37.7));
        var faq = Substitute.For<IFaqRepository>();
        faq.GetAllAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<FaqEntry>());
        IBotCommand command = name switch
        {
            "start" => new StartCommand(),
            "contacts" => new ContactsCommand(),
            "instruction" => new InstructionCommand(),
            "faq" => new FaqCommand(faq),
            "preparations" => new PreparationsListCommand(preparations),
            "pharmacies" => new PharmaciesListCommand(pharmacies),
            "prepsearch" => new PreparationSearchCommand(preparations, pharmacies),
            "prepinfo" => new PreparationDetailCommand(preparations, pharmacies),
            _ => new PharmacyMapCommand(pharmacies)
        };
        var callback = name switch
        {
            "prepsearch" => "prepsearch:begin",
            "location" => "pharmmap:1",
            _ => $"{name}:invalid"
        };
        var context = new BotContext("chat", "user", "Test", platform, Substitute.For<IUserSessionStore>());
        var execution = command.ExecuteAsync(context,
            new IncomingMessage("chat", "user", "", callback, "Test"), cancellation.Token);

        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(2));
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => execution.WaitAsync(TimeSpan.FromSeconds(1)));
        }
        finally
        {
            finished.TrySetResult();
        }
    }
}
