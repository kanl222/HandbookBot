using HandbookBot.Core.Interfaces;
using HandbookBot.Core.Models;

namespace HandbookBot.Data.InMemory;

/// <summary>
/// In-memory репозиторий препаратов с тестовыми данными.
/// Сортировка по убыванию цены (согласно ТЗ).
/// </summary>
public sealed class InMemoryPreparationRepository : IPreparationRepository
{
    private static readonly IReadOnlyList<Preparation> Data =
    [
        new(1,  "Инсулин Хумулин М3 (картридж 3мл №5)",    1_290.00m, 1, true,  "Двухфазная суспензия человеческого генно-инженерного инсулина для контроля гликемии при сахарном диабете."),
        new(2,  "Метформин 1000 мг №60 таб.",               312.50m,  1, true,  "Пероральный гипогликемический препарат из группы бигуанидов для лечения сахарного диабета 2 типа."),
        new(3,  "Амлодипин 10 мг №30 таб.",                 248.00m,  2, true,  "Блокатор кальциевых каналов для лечения артериальной гипертензии и ишемической болезни сердца."),
        new(4,  "Аторвастатин 40 мг №30 таб.",              390.00m,  2, true,  "Гиполипидемический препарат, ингибитор ГМГ-КоА-редуктазы (статин) для снижения уровня холестерина."),
        new(5,  "Эналаприл 10 мг №20 таб.",                 145.00m,  3, true,  "Ингибитор АПФ для лечения эссенциальной гипертензии и хронической сердечной недостаточности."),
        new(6,  "Сальбутамол (ингалятор) 100 мкг/доза",     520.00m,  3, false, "Бронхолитическое средство, селективный бета2-адреномиметик для купирования приступов бронхиальной астмы."),
        new(7,  "Варфарин 2.5 мг №50 таб.",                 198.00m,  1, true,  "Антикоагулянт непрямого действия для профилактики и лечения тромбозов и эмболий."),
        new(8,  "Клопидогрел 75 мг №28 таб.",               650.00m,  2, false, "Антиагрегантное средство для профилактики атеротромботических осложнений."),
        new(9,  "Омепразол 20 мг №28 капс.",                 85.00m,  4, true,  "Ингибитор протонной помпы для лечения язвенной болезни желудка и ГЭРБ."),
        new(10, "Левотироксин натрия 50 мкг №50 таб.",      210.00m,  4, true,  "Синтетический гормон щитовидной железы для заместительной терапии при гипотиреозе."),
    ];

    private static readonly IReadOnlyList<Preparation> Sorted =
        Data.OrderByDescending(p => p.Price).ToList();

    public Task<PagedResult<Preparation>> GetPageAsync(int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var items = Sorted
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Task.FromResult(new PagedResult<Preparation>(items, Sorted.Count, page, pageSize));
    }

    public Task<PagedResult<Preparation>> SearchAsync(string query, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        if (query.Length > 200)
        {
            query = query[..200];
        }

        var q = query.Trim().ToLowerInvariant();
        var filtered = Sorted
            .Where(p => p.Name.Contains(q, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var items = filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return Task.FromResult(new PagedResult<Preparation>(items, filtered.Count, page, pageSize));
    }

    public Task<Preparation?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var prep = Data.FirstOrDefault(p => p.Id == id);
        return Task.FromResult(prep);
    }
}
