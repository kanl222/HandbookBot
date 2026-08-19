using HandbookBot.Core.Models;

namespace HandbookBot.Core.Interfaces;

public interface IPreparationRepository
{
    Task<PagedResult<Preparation>> GetPageAsync(int page, int pageSize, CancellationToken ct = default);
    Task<PagedResult<Preparation>> SearchAsync(string query, int page, int pageSize, CancellationToken ct = default);
    Task<Preparation?> GetByIdAsync(int id, CancellationToken ct = default);
}
