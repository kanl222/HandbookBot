using HandbookBot.Core.Interfaces;
using HandbookBot.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace HandbookBot.Data.EfCore;

/// <summary>EF Core реализация IPreparationRepository.</summary>
public sealed class EfPreparationRepository : IPreparationRepository
{
    private readonly BotDbContext _db;

    public EfPreparationRepository(BotDbContext db) => _db = db;

    public async Task<PagedResult<Preparation>> GetPageAsync(int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var total = await _db.Preparations.CountAsync(ct);
        var items = await _db.Preparations
            .OrderByDescending(p => p.Price)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => p.ToDomain())
            .ToListAsync(ct);

        return new PagedResult<Preparation>(items, total, page, pageSize);
    }

    public async Task<PagedResult<Preparation>> SearchAsync(string query, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        if (query.Length > 200)
        {
            query = query[..200];
        }

        var q = query.Trim()
            .Replace(@"\", @"\\")
            .Replace("%", @"\%")
            .Replace("_", @"\_");
        var filtered = _db.Preparations
            .Where(p => EF.Functions.Like(p.Name, $"%{q}%", @"\"))
            .OrderByDescending(p => p.Price);

        var total = await filtered.CountAsync(ct);
        var items = await filtered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => p.ToDomain())
            .ToListAsync(ct);

        return new PagedResult<Preparation>(items, total, page, pageSize);
    }
}
