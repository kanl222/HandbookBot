using HandbookBot.Core.Interfaces;
using HandbookBot.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace HandbookBot.Data.EfCore;

/// <summary>EF Core реализация IPharmacyRepository.</summary>
public sealed class EfPharmacyRepository : IPharmacyRepository
{
    private readonly BotDbContext _db;

    public EfPharmacyRepository(BotDbContext db) => _db = db;

    public async Task<PagedResult<Pharmacy>> GetPageAsync(int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var total = await _db.Pharmacies.CountAsync(ct);
        var items = await _db.Pharmacies
            .OrderBy(p => p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => p.ToDomain())
            .ToListAsync(ct);

        return new PagedResult<Pharmacy>(items, total, page, pageSize);
    }

    public async Task<Pharmacy?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var entity = await _db.Pharmacies.FindAsync([id], ct);
        return entity?.ToDomain();
    }
}
