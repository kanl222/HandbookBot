using HandbookBot.Core.Commands;
using HandbookBot.Core.Interfaces;
using HandbookBot.Core.Models;
using HandbookBot.Tests.Helpers;
using NSubstitute;

namespace HandbookBot.Tests.Unit.Commands;

public class PreparationDetailCommandTests
{
    private readonly IPreparationRepository _prepRepo;
    private readonly IPharmacyRepository _pharmacyRepo;
    private readonly PreparationDetailCommand _sut;
    private readonly FakeMessagingPlatform _platform;
    private readonly BotContext _context;

    public PreparationDetailCommandTests()
    {
        _prepRepo = Substitute.For<IPreparationRepository>();
        _pharmacyRepo = Substitute.For<IPharmacyRepository>();
        _sut = new PreparationDetailCommand(_prepRepo, _pharmacyRepo);
        _platform = new FakeMessagingPlatform();
        var sessions = Substitute.For<IUserSessionStore>();
        _context = new BotContext("chat", "user", "Test", _platform, sessions);
    }

    [Fact]
    public async Task ExecuteAsync_WhenStocksHavePharmacyNames_DoesNotCallPharmacyRepo()
    {
        // Arrange
        var stock1 = new PreparationStock(10, "Аптека №10", "ул. Ленина, 1", 5m);
        var stock2 = new PreparationStock(20, "Аптека №20", "ул. Мира, 5", 2m);

        var prep = new Preparation(
            Id: 1,
            Name: "Парацетамол",
            Price: 50m,
            PharmacyId: 10,
            IsAvailable: true,
            Description: "Обезболивающее",
            TotalPacks: 7m,
            Stocks: new[] { stock1, stock2 });

        _prepRepo.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(prep);

        var msg = new IncomingMessage("chat", "user", "", "prepinfo:1", "Test");

        // Act
        await _sut.ExecuteAsync(_context, msg);

        // Assert: _pharmacyRepo не должен вызываться для получения названий аптек
        await _pharmacyRepo.DidNotReceive().GetByIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
        await _pharmacyRepo.DidNotReceive().GetByIdsAsync(Arg.Any<IEnumerable<int>>(), Arg.Any<CancellationToken>());

        var sent = Assert.Single(_platform.SentMessages);
        Assert.Contains("Парацетамол", sent.Text);
        Assert.Contains("• Статус: В наличии (всего 7 уп.)", sent.Text);
        Assert.NotNull(sent.Keyboard);

        // Кнопки для аптек должны содержать названия из stocks
        var buttons = sent.Keyboard.Rows.SelectMany(r => r).ToList();
        Assert.Contains(buttons, b => b.Text == "Карта: Аптека №10" && b.Payload == "pharmmap:10");
        Assert.Contains(buttons, b => b.Text == "Карта: Аптека №20" && b.Payload == "pharmmap:20");
    }

    [Fact]
    public async Task ExecuteAsync_AvailableWithoutPackCount_DoesNotInventZeroQuantity()
    {
        _prepRepo.GetByIdAsync(10, Arg.Any<CancellationToken>())
            .Returns(new Preparation(10, "Drug", 30m, 0, true));

        await _sut.ExecuteAsync(_context, new IncomingMessage("chat", "user", "", "prepinfo:10", "Test"));

        var sent = Assert.Single(_platform.SentMessages);
        Assert.Contains("• Статус: В наличии", sent.Text);
        Assert.DoesNotContain("Нет в наличии", sent.Text);
        Assert.DoesNotContain("всего", sent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_UnavailableWithStalePackCount_DoesNotClaimAvailability()
    {
        _prepRepo.GetByIdAsync(11, Arg.Any<CancellationToken>())
            .Returns(new Preparation(11, "Drug", 30m, 0, false, TotalPacks: 7m));

        await _sut.ExecuteAsync(_context, new IncomingMessage("chat", "user", "", "prepinfo:11", "Test"));

        var sent = Assert.Single(_platform.SentMessages);
        Assert.Contains("• Статус: Нет в наличии", sent.Text);
        Assert.DoesNotContain("• Статус: В наличии", sent.Text);
        Assert.DoesNotContain("всего", sent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_BatchFallback_PreservesOrderAndMissingPharmacyLabels()
    {
        var prep = new Preparation(3, "Drug", 30m, 30, true,
            PharmacyIds: new[] { 30, 10, 99 });
        _prepRepo.GetByIdAsync(3, Arg.Any<CancellationToken>()).Returns(prep);
        _pharmacyRepo.GetByIdsAsync(Arg.Any<IEnumerable<int>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<int, Pharmacy>
            {
                [10] = new Pharmacy(10, "Ten", "Address", 0, 0, ""),
                [30] = new Pharmacy(30, "Thirty", "Address", 0, 0, "")
            });

        await _sut.ExecuteAsync(_context, new IncomingMessage("chat", "user", "", "prepinfo:3", "Test"));

        var buttons = Assert.Single(_platform.SentMessages).Keyboard!.Rows.Take(3).SelectMany(row => row).ToArray();
        Assert.Equal(new[] { "Карта: Thirty", "Карта: Ten", "Аптечный пункт №99" }, buttons.Select(button => button.Text));
        Assert.Equal(new[] { "pharmmap:30", "pharmmap:10", "pharmmap:99" }, buttons.Select(button => button.Payload));
    }

    [Fact]
    public async Task ExecuteAsync_WhenStockLacksName_FallsBackToPharmacyRepo()
    {
        // Arrange: нет stocks, но есть PharmacyIds
        var prep = new Preparation(
            Id: 2,
            Name: "Аспирин",
            Price: 30m,
            PharmacyId: 30,
            IsAvailable: false,
            PharmacyIds: new[] { 30 });

        _prepRepo.GetByIdAsync(2, Arg.Any<CancellationToken>()).Returns(prep);
        _pharmacyRepo.GetByIdsAsync(Arg.Is<IEnumerable<int>>(ids => ids != null && ids.SequenceEqual(new[] { 30 })), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<int, Pharmacy>
            {
                [30] = new Pharmacy(30, "Центральная Аптека", "ул. Советская, 10", 55.0, 37.0, "+79990000000")
            });

        var msg = new IncomingMessage("chat", "user", "", "prepinfo:2", "Test");

        // Act
        await _sut.ExecuteAsync(_context, msg);

        // Assert: был fallback к репозиторию
        await _pharmacyRepo.Received(1).GetByIdsAsync(
            Arg.Is<IEnumerable<int>>(ids => ids != null && ids.SequenceEqual(new[] { 30 })), Arg.Any<CancellationToken>());

        var sent = Assert.Single(_platform.SentMessages);
        var buttons = sent.Keyboard?.Rows.SelectMany(r => r).ToList() ?? [];
        Assert.Contains(buttons, b => b.Text == "Карта: Центральная Аптека" && b.Payload == "pharmmap:30");
    }
}
