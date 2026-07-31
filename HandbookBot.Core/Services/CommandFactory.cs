using HandbookBot.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace HandbookBot.Core.Services;

/// <summary>
/// Фабрика команд — разрешает соответствующую команду бота (<see cref="IBotCommand"/>) по строковому имени через Keyed DI.
/// </summary>
public sealed class CommandFactory : ICommandFactory
{
    private readonly IServiceProvider _sp;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="CommandFactory"/>.
    /// </summary>
    /// <param name="sp">Провайдер сервисов DI.</param>
    public CommandFactory(IServiceProvider sp) => _sp = sp;

    /// <summary>
    /// Возвращает реализацию команды <see cref="IBotCommand"/> по её ключу/имени.
    /// </summary>
    /// <param name="commandName">Имя или ключ команды.</param>
    /// <returns>Экземпляр команды или null, если команда не зарегистрирована.</returns>
    public IBotCommand? Resolve(string commandName)
        => _sp.GetKeyedService<IBotCommand>(commandName);
}
