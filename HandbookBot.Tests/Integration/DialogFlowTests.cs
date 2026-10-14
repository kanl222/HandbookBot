using HandbookBot.Core.Interfaces;
using HandbookBot.Core.Models;
using HandbookBot.Tests.Helpers;
using Microsoft.Extensions.Caching.Distributed;
using NSubstitute;

namespace HandbookBot.Tests.Integration;

public class DialogFlowTests
{
    [Theory]
    [InlineData("Список препаратов", "*Препараты*")]
    [InlineData("Поиск препарата", "Введите название препарата")]
    [InlineData("Аптечные пункты", "*Аптечные пункты*")]
    [InlineData("Частые вопросы (FAQ)", "*Часто задаваемые вопросы")]
    [InlineData("Инструкция", "*Инструкция")]
    [InlineData("Контакты ГАУЗ ОАС", "*Контакты")]
    public async Task EveryMainMenuButton_OpensItsSectionAndReturnsToMenu(string label, string expected)
    {
        using var dialog = new DialogHarness();
        var menu = await dialog.SendAsync("/start");
        Assert.Equal(6, menu.Buttons.Count);
        var section = await dialog.ClickAsync(menu.Button(label));
        Assert.Contains(expected, section.Text);
        if (label == "Поиск препарата")
            Assert.Equal("prepsearch", (await dialog.StateAsync())?.AwaitingInputFor);
        else
            Assert.Null(await dialog.StateAsync());

        var returned = await dialog.ClickAsync(section.MainMenuButton());
        Assert.Contains("Добро пожаловать", returned.Text);
        Assert.Equal(6, returned.Buttons.Count);
        Assert.Null(await dialog.StateAsync());
    }

    [Fact]
    public async Task Search_PagesBothWays_OpensDetailsAndMap_AndReturnsToMenu()
    {
        using var dialog = new DialogHarness();
        var menu = await dialog.SendAsync("/start");
        await dialog.ClickAsync(menu.Button("Поиск препарата"));
        var empty = await dialog.SendAsync("   ");
        Assert.Contains("не может быть пустым", empty.Text);
        Assert.Equal("prepsearch", (await dialog.StateAsync())?.AwaitingInputFor);

        var firstPage = await dialog.SendAsync("м");
        Assert.Contains("стр. 1/2", firstPage.Text);
        Assert.Equal("м", (await dialog.StateAsync())?.SearchQuery);
        var oldNextButton = firstPage.Button("Вперёд");
        var secondPage = await dialog.ClickAsync(oldNextButton);
        Assert.Contains("стр. 2/2", secondPage.Text);
        var back = await dialog.ClickAsync(secondPage.Button("Назад"));
        Assert.Contains("стр. 1/2", back.Text);

        var details = await dialog.ClickAsync(back.Buttons.First(button => button.Payload!.StartsWith("prepinfo:")));
        Assert.Contains("Инсулин", details.Text);
        var map = await dialog.ClickAsync(details.Buttons.First(button => button.Payload!.StartsWith("pharmmap:")));
        var location = Assert.Single(dialog.Platform.SentLocations);
        Assert.Equal(54.9893, location.Latitude);
        Assert.Equal(73.3682, location.Longitude);
        Assert.Contains("Аптека №1", map.Text);

        await dialog.ClickAsync(map.MainMenuButton());
        Assert.Null(await dialog.StateAsync());
        var expired = await dialog.ClickAsync(oldNextButton);
        Assert.Contains("сессии поиска истекло", expired.Text);
        Assert.Equal("prepsearch:begin", expired.Button("Новый поиск").Payload);
    }

    [Theory]
    [InlineData("button")]
    [InlineData("/start")]
    [InlineData(" /START@HandbookBot extra ")]
    public async Task CancelSearch_ClearsTheSession_AndPlainTextNoLongerStartsSearch(string action)
    {
        using var dialog = new DialogHarness();
        await dialog.SendAsync("/start");
        var prompt = await dialog.SendAsync(callback: "prepsearch:begin");
        var menu = action == "button"
            ? await dialog.ClickAsync(prompt.Button("Отмена"))
            : await dialog.SendAsync(action);
        Assert.Contains("Добро пожаловать", menu.Text);
        Assert.Null(await dialog.StateAsync());
        var plain = await dialog.SendAsync("метформин");
        Assert.Contains("Команда не распознана", plain.Text);
        Assert.Null(await dialog.StateAsync());
    }

    [Fact]
    public async Task NoResults_CanStartANewSearch_AndReplaceThePreviousQuery()
    {
        using var dialog = new DialogHarness();
        await dialog.SendAsync(callback: "prepsearch:begin");
        var noResults = await dialog.SendAsync("несуществующий-препарат");
        Assert.Contains("ничего не найдено", noResults.Text);
        Assert.Equal("несуществующий-препарат", (await dialog.StateAsync())?.SearchQuery);
        await dialog.ClickAsync(noResults.Button("Новый поиск"));
        Assert.Null((await dialog.StateAsync())?.SearchQuery);
        var found = await dialog.SendAsync("метформин");
        Assert.Contains("Метформин", found.Text);
        var changed = await dialog.SendAsync("инсулин");
        Assert.Contains("Инсулин", changed.Text);
        Assert.DoesNotContain("Метформин", changed.Text);
        Assert.Equal("инсулин", (await dialog.StateAsync())?.SearchQuery);
    }

    [Fact]
    public async Task Search_TrimsAndLimitsTheQuery_AndBlankInputKeepsTheCurrentQuery()
    {
        using var dialog = new DialogHarness();
        await dialog.SendAsync(callback: "prepsearch:begin");
        var query = new string('x', 250);
        var result = await dialog.SendAsync("  " + query + "  ");
        Assert.Contains("ничего не найдено", result.Text);
        Assert.Equal(new string('x', 200), (await dialog.StateAsync())?.SearchQuery);
        var empty = await dialog.SendAsync(" \t ");
        Assert.Contains("не может быть пустым", empty.Text);
        Assert.Equal(new string('x', 200), (await dialog.StateAsync())?.SearchQuery);
    }

    [Theory]
    [InlineData("/preparations", "*Препараты*")]
    [InlineData("/pharmacies", "*Аптечные пункты*")]
    [InlineData("/faq", "*Часто задаваемые вопросы")]
    [InlineData("/instruction", "*Инструкция")]
    [InlineData("/contacts", "*Контакты")]
    public async Task ExplicitCommand_HasPriorityOverSearch_WithoutLosingItsSession(string command, string expected)
    {
        using var dialog = new DialogHarness();
        await dialog.SendAsync(callback: "prepsearch:begin");
        var section = await dialog.SendAsync(command);
        Assert.Contains(expected, section.Text);
        Assert.Equal("prepsearch", (await dialog.StateAsync())?.AwaitingInputFor);
        var resumed = await dialog.SendAsync("метформин");
        Assert.Contains("Результаты поиска", resumed.Text);
        await dialog.ClickAsync(resumed.MainMenuButton());
        Assert.Null(await dialog.StateAsync());
    }

    [Fact]
    public async Task PreparationCatalog_Paginates_AndDetailsLinkToPharmaciesAndMap()
    {
        using var dialog = new DialogHarness();
        var first = await dialog.SendAsync(callback: "preparations:1");
        Assert.Contains("стр. 1/2", first.Text);
        var second = await dialog.ClickAsync(first.Button("Вперёд"));
        Assert.Contains("стр. 2/2", second.Text);
        var returned = await dialog.ClickAsync(second.Button("Назад"));
        Assert.Contains("стр. 1/2", returned.Text);
        var details = await dialog.ClickAsync(returned.Buttons.First(button => button.Payload!.StartsWith("prepinfo:")));
        var pharmacies = await dialog.ClickAsync(details.Button("Все аптечные пункты"));
        Assert.Contains("*Аптечные пункты*", pharmacies.Text);
        var secondPharmacies = await dialog.ClickAsync(pharmacies.Button("Вперёд"));
        Assert.Contains("стр. 2/2", secondPharmacies.Text);
        var map = await dialog.ClickAsync(secondPharmacies.Buttons.First(button => button.Payload!.StartsWith("pharmmap:")));
        Assert.Single(dialog.Platform.SentLocations);
        Assert.Contains("Тел:", map.Text);
        var menu = await dialog.ClickAsync(map.MainMenuButton());
        Assert.Equal(6, menu.Buttons.Count);
    }

    [Fact]
    public async Task EveryFaqQuestion_OpensAnAnswer_AndReturnsToTheQuestionList()
    {
        using var dialog = new DialogHarness();
        var list = await dialog.SendAsync(callback: "faq:list");
        var questions = list.Buttons.Where(button => button.Payload!.StartsWith("faq:")).ToArray();
        Assert.Equal(5, questions.Length);
        foreach (var question in questions)
        {
            var answer = await dialog.ClickAsync(question);
            Assert.Contains(question.Text, answer.Text);
            Assert.Equal(2, answer.Buttons.Count);
            var back = await dialog.ClickAsync(answer.Button("Назад к вопросам"));
            Assert.Equal(questions.Select(button => button.Payload), back.Buttons
                .Where(button => button.Payload!.StartsWith("faq:")).Select(button => button.Payload));
        }
        var menu = await dialog.ClickAsync(list.MainMenuButton());
        Assert.Equal(6, menu.Buttons.Count);
    }

    [Theory]
    [InlineData("prepinfo:abc", "Некорректный идентификатор препарата")]
    [InlineData("prepinfo", "Некорректный идентификатор препарата")]
    [InlineData("prepinfo:99999", "Препарат не найден")]
    [InlineData("pharmmap:abc", "Некорректный идентификатор аптеки")]
    [InlineData("pharmmap", "Некорректный идентификатор аптеки")]
    [InlineData("pharmmap:99999", "Аптека не найдена")]
    public async Task InvalidOrMissingItem_HasAWorkingReturnToMenu(string callback, string expected)
    {
        using var dialog = new DialogHarness();
        var error = await dialog.SendAsync(callback: callback);
        Assert.Contains(expected, error.Text);
        var menu = await dialog.ClickAsync(error.MainMenuButton());
        Assert.Equal(6, menu.Buttons.Count);
    }

    [Fact]
    public async Task UnknownCallback_DoesNotConsumeSearchInput()
    {
        using var dialog = new DialogHarness();
        await dialog.SendAsync(callback: "prepsearch:begin");
        var unknown = await dialog.SendAsync(callback: "removed:123");
        Assert.Contains("Неизвестное действие", unknown.Text);
        Assert.Equal("prepsearch", (await dialog.StateAsync())?.AwaitingInputFor);
        var resumed = await dialog.SendAsync("метформин");
        Assert.Contains("Метформин", resumed.Text);
    }

    [Fact]
    public async Task ExpiredSearchSession_PromptsForANewSearch_AndCanRecover()
    {
        using var dialog = new DialogHarness();
        await dialog.SendAsync(callback: "prepsearch:begin");
        await dialog.SendAsync("м");
        var key = dialog.SessionKeys.Create(DialogHarness.Message());
        await dialog.Sessions.SetStateAsync(key,
            new UserDialogState("prepsearch") { SearchQuery = "м" }, TimeSpan.FromMilliseconds(1));
        await Task.Delay(20);
        Assert.Null(await dialog.StateAsync());
        var expired = await dialog.SendAsync(callback: "prepsearch:2");
        Assert.Contains("сессии поиска истекло", expired.Text);
        await dialog.ClickAsync(expired.Button("Новый поиск"));
        var found = await dialog.SendAsync("метформин");
        Assert.Contains("Метформин", found.Text);
    }

    [Fact]
    public async Task CorruptedSession_IsTreatedAsExpired_AndMenuCanResetIt()
    {
        using var dialog = new DialogHarness();
        await dialog.SendAsync(callback: "prepsearch:begin");
        var key = dialog.SessionKeys.Create(DialogHarness.Message());
        await dialog.Cache.SetStringAsync("session:" + key.ToLowerInvariant(), "{broken");
        var expired = await dialog.SendAsync(callback: "prepsearch:1");
        Assert.Contains("сессии поиска истекло", expired.Text);
        await dialog.ClickAsync(expired.MainMenuButton());
        Assert.Null(await dialog.StateAsync());
    }

    [Fact]
    public async Task SessionForARemovedCommand_IsClearedBeforeFallback()
    {
        using var dialog = new DialogHarness();
        await dialog.Sessions.SetStateAsync(dialog.SessionKeys.Create(DialogHarness.Message()),
            new UserDialogState("removed-command"));
        var response = await dialog.SendAsync("метформин");
        Assert.Contains("Команда не распознана", response.Text);
        Assert.Null(await dialog.StateAsync());
    }

    [Theory]
    [InlineData("chat", "other-user", "Max")]
    [InlineData("other-chat", "user", "Max")]
    [InlineData("chat", "user", "Telegram")]
    public async Task SearchSessions_AreIsolatedByUserChatAndPlatform(string chat, string user, string platform)
    {
        using var dialog = new DialogHarness();
        await dialog.SendAsync(callback: "prepsearch:begin");
        var other = DialogHarness.Message("метформин", chat: chat, user: user, platform: platform);
        await dialog.DispatchAsync(other);
        Assert.Contains("Команда не распознана", dialog.Platform.SentMessages.Last().Text);
        Assert.Null(await dialog.StateAsync(other));
        var own = await dialog.SendAsync("метформин");
        Assert.Contains("Метформин", own.Text);
    }

    [Fact]
    public async Task SearchFailure_ProducesAnError_AndStartRecoversTheDialog()
    {
        var repository = Substitute.For<IPreparationRepository>();
        repository.SearchAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<PagedResult<Preparation>>(new HttpRequestException("Test API failure")));
        using var dialog = new DialogHarness(repository);
        await dialog.SendAsync(callback: "prepsearch:begin");
        await dialog.DispatchAsync(DialogHarness.Message("метформин"));
        Assert.Contains("Произошла внутренняя ошибка", dialog.Platform.SentMessages.Last().Text);
        var menu = await dialog.SendAsync("/start");
        Assert.Equal(6, menu.Buttons.Count);
        Assert.Null(await dialog.StateAsync());
    }

    [Fact]
    public async Task TimedOutSearch_ReleasesTheUserQueue_AndStartClearsItsSession()
    {
        var repository = Substitute.For<IPreparationRepository>();
        repository.SearchAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                await Task.Delay(Timeout.Infinite, call.Arg<CancellationToken>());
                return new PagedResult<Preparation>(Array.Empty<Preparation>(), 0, 1, 5);
            });
        using var dialog = new DialogHarness(repository, timeoutSeconds: 1);
        await dialog.SendAsync(callback: "prepsearch:begin");
        var response = await dialog.SendAsync("метформин").WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Contains("слишком много времени", response.Text);
        Assert.Equal("метформин", (await dialog.StateAsync())?.SearchQuery);
        var menu = await dialog.SendAsync("/start").WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(6, menu.Buttons.Count);
        Assert.Null(await dialog.StateAsync());
    }

    [Fact]
    public async Task RateLimitedSearch_DoesNotOverwriteTheSession_AndResumesAfterTheWindow()
    {
        var clock = new ManualTimeProvider();
        using var dialog = new DialogHarness(maxMessages: 3, clock: clock);
        await dialog.SendAsync("/start");
        await dialog.SendAsync(callback: "prepsearch:begin");
        await dialog.SendAsync(" ");
        var throttled = await dialog.SendAsync("метформин");
        Assert.Contains("Слишком много запросов", throttled.Text);
        Assert.Null((await dialog.StateAsync())?.SearchQuery);
        clock.Advance(TimeSpan.FromSeconds(61));
        var resumed = await dialog.SendAsync("метформин");
        Assert.Contains("Метформин", resumed.Text);
    }

    [Fact]
    public async Task SearchInput_WaitsForItsPrompt_WhileAnotherUserCanOpenTheMenu()
    {
        using var dialog = new DialogHarness();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var blockedTransport = Substitute.For<IMessagingPlatform>();
        blockedTransport.SendTextAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<BotKeyboard?>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                entered.TrySetResult();
                await release.Task.WaitAsync(call.Arg<CancellationToken>());
            });
        var begin = dialog.DispatchAsync(DialogHarness.Message(callback: "prepsearch:begin"), blockedTransport);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var search = dialog.DispatchAsync(DialogHarness.Message("метформин"));
        try
        {
            Assert.False(search.IsCompleted);
            await dialog.DispatchAsync(DialogHarness.Message("/start", user: "other-user"));
            Assert.Contains("Добро пожаловать", Assert.Single(dialog.Platform.SentMessages).Text);
        }
        finally
        {
            release.TrySetResult();
        }
        await Task.WhenAll(begin, search).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Contains("Метформин", dialog.Platform.SentMessages.Last().Text);
        Assert.Equal("метформин", (await dialog.StateAsync())?.SearchQuery);
    }

    [Theory]
    [InlineData("инсулин", true)]
    [InlineData("сальбутамол", false)]
    public async Task AvailabilityInSearch_AndDrugDetails_IsConsistent(string query, bool available)
    {
        using var dialog = new DialogHarness();
        await dialog.SendAsync(callback: "prepsearch:begin");
        var results = await dialog.SendAsync(query);
        Assert.Contains(available ? "В наличии" : "Нет в наличии", results.Text);
        var details = await dialog.ClickAsync(Assert.Single(results.Buttons, button => button.Payload!.StartsWith("prepinfo:")));
        Assert.Contains(available ? "• Статус: В наличии" : "• Статус: Нет в наличии", details.Text);
    }
}
