using HandbookBot.Core.Models;

namespace HandbookBot.Core.Interfaces;

public interface IUserSessionStore
{
    Task<UserDialogState?> GetStateAsync(string sessionKey, CancellationToken ct = default);
    Task SetStateAsync(string sessionKey, UserDialogState state, TimeSpan? ttl = null, CancellationToken ct = default);
    Task ClearStateAsync(string sessionKey, CancellationToken ct = default);
}

