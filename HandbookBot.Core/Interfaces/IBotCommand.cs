using HandbookBot.Core.Models;

namespace HandbookBot.Core.Interfaces;

/// <summary>Команда бота, адресованная конкретному чату.</summary>
public interface IBotCommand
{
    string Name { get; }

    Task ExecuteAsync(BotContext context, IncomingMessage message, CancellationToken ct = default);
}
