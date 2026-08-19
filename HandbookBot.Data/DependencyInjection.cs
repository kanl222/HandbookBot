using HandbookBot.Core.Interfaces;
using HandbookBot.Data.Api;
using HandbookBot.Data.Caching;
// using HandbookBot.Data.EfCore;    // ← DB отключён, раскомментировать при необходимости
using HandbookBot.Data.InMemory;
// using Microsoft.EntityFrameworkCore; // ← DB отключён
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HandbookBot.Data;

public static class DependencyInjection
{
    /// <summary>
    /// Регистрирует репозитории данных.
    /// Режим выбирается через конфиг "Data:Source":
    ///   "api"      → HTTP-клиент к внешнему API (требует "Data:ApiBaseUrl")
    ///   "inmemory" → InMemory-заглушки (по умолчанию)
    ///   "database" → EF Core / PostgreSQL (требует "Data:ConnectionString") — временно отключено
    /// </summary>
    public static IServiceCollection AddHandbookData(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var source = (configuration["Data:Source"] ?? "inmemory").Trim().ToLowerInvariant();

        // ── API-режим (RefInfoAPI + JWT) ───────────────────────────────────────
        if (source == "api")
        {
            // Читаем настройки подключения к RefInfoAPI
            services.Configure<RefInfoApiOptions>(configuration.GetSection(RefInfoApiOptions.Section));

            var baseUrl = configuration[$"{RefInfoApiOptions.Section}:BaseUrl"]
                ?? configuration["Data:ApiBaseUrl"]   // обратная совместимость
                ?? throw new InvalidOperationException(
                    $"Конфиг {RefInfoApiOptions.Section}:BaseUrl обязателен при Data:Source=api.");

            // JwtTokenProvider — Singleton, хранит кэш токена
            services.AddSingleton<JwtTokenProvider>();
            // JwtAuthHandler — Transient для IHttpClientFactory
            services.AddTransient<JwtAuthHandler>();

            void ConfigureClient(IHttpClientBuilder b) => b
                .ConfigureHttpClient(client =>
                {
                    client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
                    client.Timeout = TimeSpan.FromSeconds(15);
                })
                .AddHttpMessageHandler<JwtAuthHandler>();

            ConfigureClient(services.AddHttpClient<ApiPreparationRepository>());
            ConfigureClient(services.AddHttpClient<ApiPharmacyRepository>());
            ConfigureClient(services.AddHttpClient<ApiFaqRepository>());

            services.AddScoped<IPreparationRepository>(sp => sp.GetRequiredService<ApiPreparationRepository>());

            // Для аптек и FAQ оборачиваем кэшем
            services.AddScoped<IPharmacyRepository>(sp =>
            {
                IPharmacyRepository inner = sp.GetRequiredService<ApiPharmacyRepository>();
                var cache = sp.GetRequiredService<Microsoft.Extensions.Caching.Distributed.IDistributedCache>();
                return new CachedPharmacyRepository(inner, cache);
            });
            services.AddScoped<IFaqRepository>(sp =>
            {
                IFaqRepository inner = sp.GetRequiredService<ApiFaqRepository>();
                var cache = sp.GetRequiredService<Microsoft.Extensions.Caching.Distributed.IDistributedCache>();
                return new CachedFaqRepository(inner, cache);
            });

            return services;
        }

        // ── DATABASE-режим (временно отключён) ────────────────────────────────
        // if (source == "database")
        // {
        //     var connectionString = configuration["Data:ConnectionString"]
        //         ?? throw new InvalidOperationException("Data:ConnectionString is required.");
        //
        //     services.AddDbContextPool<BotDbContext>((_, options) =>
        //         options.UseNpgsql(connectionString));
        //
        //     services.AddScoped<EfPreparationRepository>();
        //     services.AddScoped<EfPharmacyRepository>();
        //     services.AddScoped<EfFaqRepository>();
        //
        //     services.AddScoped<IPreparationRepository, EfPreparationRepository>();
        //     services.AddScoped<IPharmacyRepository>(sp =>
        //     {
        //         IPharmacyRepository inner = sp.GetRequiredService<EfPharmacyRepository>();
        //         var cache = sp.GetRequiredService<Microsoft.Extensions.Caching.Distributed.IDistributedCache>();
        //         return new CachedPharmacyRepository(inner, cache);
        //     });
        //     services.AddScoped<IFaqRepository>(sp =>
        //     {
        //         IFaqRepository inner = sp.GetRequiredService<EfFaqRepository>();
        //         var cache = sp.GetRequiredService<Microsoft.Extensions.Caching.Distributed.IDistributedCache>();
        //         return new CachedFaqRepository(inner, cache);
        //     });
        //
        //     return services;
        // }

        // ── InMemory-режим (заглушка, по умолчанию) ──────────────────────────
        // Нужен DbContext-стаб, чтобы DI не падал на неиспользуемых зависимостях EF
        // services.AddDbContextPool<BotDbContext>((_, options) =>
        //     options.UseInMemoryDatabase("HandbookBotEf_Stub"));

        services.AddScoped<IPreparationRepository, InMemoryPreparationRepository>();

        services.AddScoped<IPharmacyRepository>(sp =>
        {
            IPharmacyRepository inner = sp.GetRequiredService<InMemoryPharmacyRepository>();
            var cache = sp.GetRequiredService<Microsoft.Extensions.Caching.Distributed.IDistributedCache>();
            return new CachedPharmacyRepository(inner, cache);
        });
        services.AddScoped<InMemoryPharmacyRepository>();

        services.AddScoped<IFaqRepository>(sp =>
        {
            IFaqRepository inner = sp.GetRequiredService<InMemoryFaqRepository>();
            var cache = sp.GetRequiredService<Microsoft.Extensions.Caching.Distributed.IDistributedCache>();
            return new CachedFaqRepository(inner, cache);
        });
        services.AddScoped<InMemoryFaqRepository>();

        return services;
    }
}
