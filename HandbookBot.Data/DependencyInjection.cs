using HandbookBot.Core.Interfaces;
using HandbookBot.Data.Caching;
using HandbookBot.Data.EfCore;
using HandbookBot.Data.InMemory;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HandbookBot.Data;

public static class DependencyInjection
{
    /// <summary>
    /// Регистрирует репозитории данных.
    /// Переключение между in-memory и EF Core через конфиг:
    ///   "Data:UseDatabase": false  →  InMemory (заглушки)
    ///   "Data:UseDatabase": true   →  EF Core (требует "Data:ConnectionString")
    public static IServiceCollection AddHandbookData(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var useDatabaseStr = configuration["Data:UseDatabase"];
        var useDatabase = bool.TryParse(useDatabaseStr, out var parsed) && parsed;

        services.AddDbContextPool<BotDbContext>((sp, options) =>
        {
            if (useDatabase)
            {
                var connectionString = configuration["Data:ConnectionString"]
                    ?? throw new InvalidOperationException("Data:ConnectionString is required.");

                options.UseNpgsql(connectionString);
            }
            else
            {
                options.UseInMemoryDatabase("HandbookBotEf_Stub");
            }
        });

        // Регистрируем и EF, и InMemory репозитории
        services.AddScoped<EfPreparationRepository>();
        services.AddScoped<InMemoryPreparationRepository>();
        services.AddScoped<IPreparationRepository>(sp =>
        {
            if (useDatabase)
                return ActivatorUtilities.CreateInstance<EfPreparationRepository>(sp);
            return ActivatorUtilities.CreateInstance<InMemoryPreparationRepository>(sp);
        });

        services.AddScoped<EfPharmacyRepository>();
        services.AddScoped<InMemoryPharmacyRepository>();
        services.AddScoped<IPharmacyRepository>(sp =>
        {
            IPharmacyRepository inner = useDatabase
                ? sp.GetRequiredService<EfPharmacyRepository>()
                : sp.GetRequiredService<InMemoryPharmacyRepository>();

            var cache = sp.GetRequiredService<Microsoft.Extensions.Caching.Distributed.IDistributedCache>();
            return new CachedPharmacyRepository(inner, cache);
        });

        services.AddScoped<EfFaqRepository>();
        services.AddScoped<InMemoryFaqRepository>();
        services.AddScoped<IFaqRepository>(sp =>
        {
            IFaqRepository inner = useDatabase
                ? sp.GetRequiredService<EfFaqRepository>()
                : sp.GetRequiredService<InMemoryFaqRepository>();

            var cache = sp.GetRequiredService<Microsoft.Extensions.Caching.Distributed.IDistributedCache>();
            return new CachedFaqRepository(inner, cache);
        });

        return services;
    }
}
