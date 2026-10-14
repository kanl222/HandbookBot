using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HandbookBot.Core.Interfaces;
using HandbookBot.Core.Models;
using HandbookBot.Data.Api;
using HandbookBot.Data.Caching;
using HandbookBot.Tests.Helpers;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace HandbookBot.Tests.Unit.Services;

public class PharmacyBatchTests
{
    [Fact]
    public async Task ApiBatch_LoadsAtMostFourPharmaciesAtOnceAndSkipsDuplicatesAndMissingIds()
    {
        var fourStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sync = new object();
        var active = 0;
        var maximum = 0;
        var calls = 0;
        using var handler = new StubHttpMessageHandler(async (request, ct) =>
        {
            var id = int.Parse(request.RequestUri!.Segments.Last());
            Interlocked.Increment(ref calls);
            var count = Interlocked.Increment(ref active);
            lock (sync)
                maximum = Math.Max(maximum, count);
            if (count == 4)
                fourStarted.TrySetResult();
            try
            {
                await release.Task.WaitAsync(ct);
                return id == 99
                    ? new HttpResponseMessage(HttpStatusCode.NotFound)
                    : new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = JsonContent.Create(new ApiPharmacy { Id = id, Name = $"Pharmacy {id}" })
                    };
            }
            finally
            {
                Interlocked.Decrement(ref active);
            }
        });
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://unit.test/") };
        var repository = new ApiPharmacyRepository(client);
        var batch = repository.GetByIdsAsync(Enumerable.Range(1, 12).Concat(new[] { 99, 1, 3 }));
        try
        {
            await fourStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(4, Volatile.Read(ref calls));
        }
        finally
        {
            release.TrySetResult();
        }
        var result = await batch.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(4, maximum);
        Assert.Equal(13, calls);
        Assert.Equal(12, result.Count);
        Assert.False(result.ContainsKey(99));
        Assert.Equal("Pharmacy 3", result[3].Name);
    }

    [Fact]
    public async Task ApiBatch_CancellationStopsEveryInFlightRequest()
    {
        using var cancellation = new CancellationTokenSource();
        var fourStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var active = 0;
        using var handler = new StubHttpMessageHandler(async (_, ct) =>
        {
            if (Interlocked.Increment(ref active) == 4)
                fourStarted.TrySetResult();
            try
            {
                await Task.Delay(Timeout.Infinite, ct);
                return new HttpResponseMessage(HttpStatusCode.OK);
            }
            finally
            {
                Interlocked.Decrement(ref active);
            }
        });
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://unit.test/") };
        var repository = new ApiPharmacyRepository(client);
        var batch = repository.GetByIdsAsync(Enumerable.Range(1, 12), cancellation.Token);
        try
        {
            await fourStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            cancellation.Cancel();
        }
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => batch.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(0, active);
    }

    [Fact]
    public async Task DefaultBatch_RemainsSequentialForRepositoriesWithSharedState()
    {
        var repository = new SequentialRepository();
        var result = await ((IPharmacyRepository)repository).GetByIdsAsync(new[] { 3, 1, 3, 99 });
        Assert.Equal(1, repository.Maximum);
        Assert.Equal(new[] { 3, 1, 99 }, repository.Calls);
        Assert.Equal(new[] { 1, 3 }, result.Keys.OrderBy(id => id));
    }

    [Fact]
    public async Task CachedBatch_LoadsOnlyMissesAndPopulatesTheIndividualCache()
    {
        var cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        await cache.SetStringAsync("Pharmacy_1", JsonSerializer.Serialize(TestData.CreatePharmacy(1, "Cached", "Address")));
        await cache.SetStringAsync("Pharmacy_2", "{broken");
        var inner = Substitute.For<IPharmacyRepository>();
        inner.GetByIdsAsync(Arg.Is<IEnumerable<int>>(ids => ids != null && ids.SequenceEqual(new[] { 2, 99 })),
                Arg.Any<CancellationToken>())
            .Returns(new Dictionary<int, Pharmacy> { [2] = TestData.CreatePharmacy(2, "Loaded", "Address") });
        var repository = new CachedPharmacyRepository(inner, cache);

        var first = await repository.GetByIdsAsync(new[] { 1, 2, 2, 99 });
        Assert.Equal(2, first.Count);
        Assert.Equal("Cached", first[1].Name);
        Assert.Equal("Loaded", first[2].Name);
        Assert.False(first.ContainsKey(99));

        var second = await repository.GetByIdsAsync(new[] { 2, 1 });
        var single = await repository.GetByIdAsync(2);
        Assert.Equal("Loaded", second[2].Name);
        Assert.Equal("Loaded", single!.Name);
        await inner.Received(1).GetByIdsAsync(Arg.Any<IEnumerable<int>>(), Arg.Any<CancellationToken>());
        await inner.DidNotReceive().GetByIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    private sealed class SequentialRepository : IPharmacyRepository
    {
        private int _active;
        public int Maximum { get; private set; }
        public List<int> Calls { get; } = new();

        public Task<PagedResult<Pharmacy>> GetPageAsync(int page, int pageSize, CancellationToken ct = default)
            => throw new NotSupportedException();

        public async Task<Pharmacy?> GetByIdAsync(int id, CancellationToken ct = default)
        {
            Maximum = Math.Max(Maximum, Interlocked.Increment(ref _active));
            try
            {
                Calls.Add(id);
                await Task.Yield();
                ct.ThrowIfCancellationRequested();
                return id == 99 ? null : TestData.CreatePharmacy(id, $"Pharmacy {id}", "Address");
            }
            finally
            {
                Interlocked.Decrement(ref _active);
            }
        }
    }
}
