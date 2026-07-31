using HandbookBot.Core.Interfaces;
using HandbookBot.Core.Models;

namespace HandbookBot.Data.InMemory;

/// <summary>In-memory репозиторий аптечных пунктов с тестовыми данными.</summary>
public sealed class InMemoryPharmacyRepository : IPharmacyRepository
{
    private static readonly IReadOnlyList<Pharmacy> Data =
    [
        new(1, "Аптека №1 ГАУЗ ОАС",          "г. Омск, ул. Ленина, 1",              54.9893,  73.3682, "+7 (3812) 55-01-01"),
        new(2, "Аптека №2 ГАУЗ ОАС",          "г. Омск, пр. Маркса, 34",             54.9766,  73.3791, "+7 (3812) 55-02-02"),
        new(3, "Аптека №3 ГАУЗ ОАС (Левый б.)", "г. Омск, ул. Герцена, 66",          54.9571,  73.4026, "+7 (3812) 55-03-03"),
        new(4, "Аптека №4 ГАУЗ ОАС",          "г. Омск, ул. Красный Путь, 107",      54.9710,  73.3511, "+7 (3812) 55-04-04"),
        new(5, "Аптека №5 ГАУЗ ОАС (Нефтяники)", "г. Омск, ул. 70 лет Октября, 17", 54.9944,  73.4315, "+7 (3812) 55-05-05"),
        new(6, "Аптека №6 ГАУЗ ОАС (Советский р-н)", "г. Омск, ул. Рокоссовского, 3", 55.0102, 73.3897, "+7 (3812) 55-06-06"),
    ];

    public Task<PagedResult<Pharmacy>> GetPageAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var items = Data
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Task.FromResult(new PagedResult<Pharmacy>(items, Data.Count, page, pageSize));
    }

    public Task<Pharmacy?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var pharmacy = Data.FirstOrDefault(p => p.Id == id);
        return Task.FromResult(pharmacy);
    }
}
