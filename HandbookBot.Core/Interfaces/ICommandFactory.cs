namespace HandbookBot.Core.Interfaces;

/// <summary>Фабрика команд — разрешает команду по имени (через keyed DI).</summary>
public interface ICommandFactory
{
    IBotCommand? Resolve(string commandName);
}
