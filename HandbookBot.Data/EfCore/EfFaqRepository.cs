using HandbookBot.Core.Interfaces;
using HandbookBot.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace HandbookBot.Data.EfCore;

/// <summary>EF Core реализация IFaqRepository.</summary>
public sealed class EfFaqRepository : IFaqRepository
{
    private readonly BotDbContext _db;

    public EfFaqRepository(BotDbContext db) => _db = db;

    public async Task<IReadOnlyList<FaqEntry>> GetAllAsync(CancellationToken ct = default)
    {
        return await _db.FaqEntries
            .OrderBy(f => f.Id)
            .Select(f => f.ToDomain())
            .ToListAsync(ct);
    }
}
