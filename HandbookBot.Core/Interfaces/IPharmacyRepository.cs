using HandbookBot.Core.Models;

namespace HandbookBot.Core.Interfaces;

public interface IPharmacyRepository
{
    Task<PagedResult<Pharmacy>> GetPageAsync(int page, int pageSize, CancellationToken ct = default);
    Task<Pharmacy?> GetByIdAsync(int id, CancellationToken ct = default);

    // По умолчанию запросы последовательны: один EF DbContext нельзя использовать параллельно.
    async Task<IReadOnlyDictionary<int, Pharmacy>> GetByIdsAsync(IEnumerable<int> ids, CancellationToken ct = default)
    {
        var result = new Dictionary<int, Pharmacy>();
        foreach (var id in ids.Distinct())
        {
            ct.ThrowIfCancellationRequested();
            var pharmacy = await GetByIdAsync(id, ct);
            if (pharmacy is not null)
                result[id] = pharmacy;
        }
        return result;
    }
}
