using HandbookBot.Core.Models;

namespace HandbookBot.Core.Interfaces;

public interface IPharmacyRepository
{
    Task<PagedResult<Pharmacy>> GetPageAsync(int page, int pageSize, CancellationToken ct = default);
    Task<Pharmacy?> GetByIdAsync(int id, CancellationToken ct = default);
}
