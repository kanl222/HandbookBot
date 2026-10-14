using BotEngine.Core;
using HandbookBot.Core;
using HandbookBot.Core.Interfaces;
using HandbookBot.Data;
using HandbookBot.Data.Caching;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HandbookBot.Tests.Helpers;

/// <summary>Настоящие DI, диспетчер, middleware, команды и кэш; транспорт записывает ответы в память.</summary>
internal sealed class DialogHarness : IDisposable
{
    private readonly ServiceProvider _services;
    public FakeMessagingPlatform Platform { get; } = new();
    public IUserSessionStore Sessions => _services.GetRequiredService<IUserSessionStore>();
    public IDistributedCache Cache => _services.GetRequiredService<IDistributedCache>();
    public ISessionKeyProvider SessionKeys => _services.GetRequiredService<ISessionKeyProvider>();

    public DialogHarness(IPreparationRepository? preparations = null, int timeoutSeconds = 60,
        int maxMessages = 1000, TimeProvider? clock = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Data:Source"] = "inmemory",
            ["BotEngine:InstanceId"] = "dialog-tests",
            ["BotEngine:RateLimit:MaxMessages"] = maxMessages.ToString(),
            ["HandbookBot:MessageTimeoutSeconds"] = timeoutSeconds.ToString()
        }).Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddHandbookData(configuration);
        services.AddBotEngine();
        services.AddHandbookMessagePipeline();
        services.AddDistributedMemoryCache();
        services.AddSingleton<IUserSessionStore, DistributedCacheUserSessionStore>();
        services.AddBotCommands();
        if (preparations is not null)
            services.Replace(ServiceDescriptor.Singleton<IPreparationRepository>(preparations));
        if (clock is not null)
            services.AddSingleton(clock);
        _services = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
    }

    public static IncomingMessage Message(string text = "", string? callback = null,
        string chat = "chat", string user = "user", string platform = "Max")
        => new(chat, user, text, callback, platform);

    public async Task DispatchAsync(IncomingMessage message, IMessagingPlatform? platform = null,
        CancellationToken ct = default)
    {
        using var scope = _services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ICommandDispatcher>()
            .DispatchAsync(message, platform ?? Platform, ct);
    }

    public Task<UserDialogState?> StateAsync(IncomingMessage? message = null)
        => Sessions.GetStateAsync(SessionKeys.Create(message ?? Message()));

    public async Task<DialogReply> SendAsync(string text = "", string? callback = null)
    {
        var count = Platform.SentMessages.Count;
        await DispatchAsync(Message(text, callback));
        var response = Assert.Single(Platform.SentMessages.Skip(count));
        Assert.DoesNotContain("Произошла внутренняя ошибка", response.Text);
        return new DialogReply(response.Text, response.Keyboard);
    }

    public Task<DialogReply> ClickAsync(BotButton button)
    {
        Assert.Equal(BotButtonType.Callback, button.Type);
        Assert.False(string.IsNullOrWhiteSpace(button.Payload));
        return SendAsync(callback: button.Payload);
    }

    public void Dispose() => _services.Dispose();
}

internal sealed record DialogReply(string Text, BotKeyboard? Keyboard)
{
    public IReadOnlyList<BotButton> Buttons => Keyboard?.Rows.SelectMany(row => row).ToArray() ?? [];
    public BotButton Button(string label) => Assert.Single(Buttons, button => button.Text == label);
    public BotButton MainMenuButton() => Assert.Single(Buttons, button => button.Payload == "start:menu");
}
