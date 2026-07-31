using HandbookBot.Core.Models;

namespace HandbookBot.Core.Interfaces;

public interface IUserSessionStore
{
    Task<UserDialogState?> GetStateAsync(string userId, CancellationToken ct = default);
    Task SetStateAsync(string userId, UserDialogState state, TimeSpan? ttl = null, CancellationToken ct = default);
    Task ClearStateAsync(string userId, CancellationToken ct = default);
}
