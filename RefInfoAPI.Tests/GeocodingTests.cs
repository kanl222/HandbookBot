using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using RefInfoAPI.Core.Interfaces;
using RefInfoAPI.Data;
using RefInfoAPI.Data.Efcore;
using RefInfoAPI.Data.Entities;
using RefInfoAPI.Data.Services;

namespace RefInfoAPI.Tests;

public class GeocodingTests
{
    [Fact]
    public async Task SqliteCache_PreservesCoordinatesAcrossServiceInstances_AndTracksStoreId()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var services = new ServiceCollection();
        services.AddDbContext<GeoCacheDbContext>(options => options.UseSqlite(connection));
        using var provider = services.BuildServiceProvider();
        using (var scope = provider.CreateScope())
            await scope.ServiceProvider.GetRequiredService<GeoCacheDbContext>().Database.EnsureCreatedAsync();
        var calls = 0;
        using var handler = new StubHandler((_, _) =>
        {
            calls++;
            return Task.FromResult(Response("37.7 55.5"));
        });
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Yandex:Geocoder:ApiKey"] = "test-key",
            ["Yandex:Geocoder:BaseUrl"] = "https://geocoder.unit.test/"
        }).Build();
        using var firstCache = new MemoryCache(new MemoryCacheOptions());
        using var secondCache = new MemoryCache(new MemoryCacheOptions());
        var scopes = provider.GetRequiredService<IServiceScopeFactory>();
        var first = new YandexGeocodingService(new StubClientFactory(handler), firstCache, configuration,
            NullLogger<YandexGeocodingService>.Instance, scopes);
        var second = new YandexGeocodingService(new StubClientFactory(handler), secondCache, configuration,
            NullLogger<YandexGeocodingService>.Instance, scopes);
        Assert.NotNull(await first.GetCoordinatesAsync("Test Street", drugstoreId: 7));
        Assert.NotNull(await second.GetCoordinatesAsync("test street", drugstoreId: 7));
        Assert.Equal(1, calls);
        using var verification = provider.CreateScope();
        var entry = await verification.ServiceProvider.GetRequiredService<GeoCacheDbContext>().GeoCacheEntries.SingleAsync();
        Assert.Equal(7, entry.DrugstoreId);
        Assert.True(entry.IsFound);
        // Изменение адреса той же аптеки требует нового результата, а не старых координат по ID.
        Assert.NotNull(await second.GetCoordinatesAsync("New Street", drugstoreId: 7));
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task Geocoder_ParsesLongitudeLatitude_AndCachesNormalizedAddresses()
    {
        var calls = 0;
        using var handler = new StubHandler((_, _) =>
        {
            Interlocked.Increment(ref calls);
            return Task.FromResult(Response("37.7 55.5"));
        });
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = Create(handler, cache);
        Assert.Equal(new PharmacyCoordinates(55.5, 37.7), await service.GetCoordinatesAsync("  Test   Street "));
        Assert.Equal(new PharmacyCoordinates(55.5, 37.7), await service.GetCoordinatesAsync("test street"));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task MissingKey_DoesNotSendARequest()
    {
        var called = false;
        using var handler = new StubHandler((_, _) =>
        {
            called = true;
            throw new InvalidOperationException("Unexpected HTTP request");
        });
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = Create(handler, cache, key: "");
        Assert.Null(await service.GetCoordinatesAsync("Test Street"));
        Assert.False(called);
    }

    [Theory]
    [InlineData("NaN 55")]
    [InlineData("181 55")]
    [InlineData("37 91")]
    [InlineData("0 0")]
    [InlineData("not coordinates")]
    public async Task InvalidProviderCoordinates_AreNotReturned(string position)
    {
        using var handler = new StubHandler((_, _) => Task.FromResult(Response(position)));
        using var cache = new MemoryCache(new MemoryCacheOptions());
        Assert.Null(await Create(handler, cache).GetCoordinatesAsync("Test Street"));
    }

    [Fact]
    public async Task TemporaryProviderError_IsNotCached()
    {
        var calls = 0;
        using var handler = new StubHandler((_, _) => Task.FromResult(
            Interlocked.Increment(ref calls) == 1
                ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                : Response("37.7 55.5")));
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = Create(handler, cache);
        Assert.Null(await service.GetCoordinatesAsync("Test Street"));
        Assert.NotNull(await service.GetCoordinatesAsync("Test Street"));
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task Cancellation_StopsTheRequest_AndReleasesTheConcurrencySlot()
    {
        using var cancellation = new CancellationTokenSource();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        using var handler = new StubHandler(async (_, ct) =>
        {
            if (Interlocked.Increment(ref calls) == 1)
            {
                started.TrySetResult();
                await Task.Delay(Timeout.Infinite, ct);
            }
            return Response("37.7 55.5");
        });
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = Create(handler, cache);
        var first = service.GetCoordinatesAsync("Test Street", cancellation.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        Assert.NotNull(await service.GetCoordinatesAsync("Test Street"));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{\"response\":null}")]
    [InlineData("{\"response\":{\"GeoObjectCollection\":null}}")]
    public async Task UnexpectedJsonShape_DoesNotProduceCoordinates(string json)
    {
        using var handler = new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        }));
        using var cache = new MemoryCache(new MemoryCacheOptions());
        Assert.Null(await Create(handler, cache).GetCoordinatesAsync("Test Street"));
    }

    [Fact]
    public async Task Geocoder_LimitsConcurrentRequestsToFour()
    {
        var fourStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var active = 0;
        var maximum = 0;
        var sync = new object();
        using var handler = new StubHandler(async (_, ct) =>
        {
            var count = Interlocked.Increment(ref active);
            lock (sync) maximum = Math.Max(maximum, count);
            if (count == 4) fourStarted.TrySetResult();
            try
            {
                await release.Task.WaitAsync(ct);
                return Response("37.7 55.5");
            }
            finally { Interlocked.Decrement(ref active); }
        });
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = Create(handler, cache);
        var requests = Enumerable.Range(1, 12).Select(id => service.GetCoordinatesAsync($"Street {id}")).ToArray();
        try
        {
            await fourStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(4, Volatile.Read(ref active));
        }
        finally { release.TrySetResult(); }
        var coordinates = await Task.WhenAll(requests).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(4, maximum);
        Assert.All(coordinates, coordinate => Assert.NotNull(coordinate));
    }

    [Fact]
    public async Task Repository_ReturnsResolvedCoordinates_AndStablePages()
    {
        var options = new DbContextOptionsBuilder<RefInfoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new RefInfoDbContext(options);
        db.Drugstores.AddRange(new Drugstore { Id = 3, Name = "Three", Address = "Address 3" },
            new Drugstore { Id = 1, Name = "One", Address = "Address 1" },
            new Drugstore { Id = 2, Name = "Two", Address = "Address 2" });
        await db.SaveChangesAsync();
        var geocoder = Substitute.For<IGeocodingService>();
        geocoder.GetCoordinatesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<int?>())
            .Returns((PharmacyCoordinates?)new PharmacyCoordinates(55.5, 37.7));
        var repository = new EfDrugStoreRepository(db, geocoder);

        var page = await repository.GetPageAsync(1, 2);
        Assert.Equal(new[] { 1, 2 }, page.Items.Select(store => store.Id));
        Assert.Equal(3, page.TotalCount);
        Assert.All(page.Items, store => Assert.Equal(55.5, store.Latitude));
        var single = await repository.GetByIdAsync(3);
        Assert.Equal(37.7, single!.Longitude);
        Assert.Null(await repository.GetByIdAsync(99));
    }

    private static YandexGeocodingService Create(HttpMessageHandler handler, IMemoryCache cache, string key = "test-key")
        => new(new StubClientFactory(handler), cache,
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Yandex:Geocoder:ApiKey"] = key,
                ["Yandex:Geocoder:BaseUrl"] = "https://geocoder.unit.test/"
            }).Build(), NullLogger<YandexGeocodingService>.Instance);

    private static HttpResponseMessage Response(string position)
        => new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new
            {
                response = new
                {
                    GeoObjectCollection = new
                    {
                        featureMember = new[] { new { GeoObject = new { Point = new { pos = position } } } }
                    }
                }
            }, options: new JsonSerializerOptions())
        };
}
