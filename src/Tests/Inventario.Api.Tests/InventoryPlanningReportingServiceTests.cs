using System.Collections;
using System.Linq.Expressions;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.DataContext;
using XFramework.Inventario.Api.Services;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Planning;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Reports;
using XFramework.Inventario.Domain.Shared.Enums;

namespace Inventario.Api.Tests;

[TestFixture]
public sealed class InventoryPlanningReportingServiceTests
{
    [Test]
    public async Task GetReorderSuggestionsAsync_AvailableBelowReorderPoint_ReturnsSuggestedQuantity()
    {
        var tenantId = Guid.NewGuid();
        var ids = TestIds.Create();
        var dataContext = SeedPlanningData(tenantId, ids, availableQuantity: 3);
        var service = CreatePlanningService(dataContext, tenantId);

        var result = await service.GetReorderSuggestionsAsync(new GetReorderSuggestionsRequest());

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Data.Should().ContainSingle();
        var suggestion = result.Data![0];
        suggestion.ProductId.Should().Be(ids.ProductId);
        suggestion.AvailableQuantity.Should().Be(3);
        suggestion.ReorderPoint.Should().Be(5);
        suggestion.SuggestedQuantity.Should().Be(17);
        suggestion.PreferredSupplier.Should().Be("Acme Supply");
    }

    [Test]
    public async Task GetReorderSuggestionsAsync_AvailableAboveReorderPoint_ReturnsEmpty()
    {
        var tenantId = Guid.NewGuid();
        var ids = TestIds.Create();
        var dataContext = SeedPlanningData(tenantId, ids, availableQuantity: 6);
        var service = CreatePlanningService(dataContext, tenantId);

        var result = await service.GetReorderSuggestionsAsync(new GetReorderSuggestionsRequest());

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Data.Should().BeEmpty();
    }

    [Test]
    public async Task CreateRuleAsync_DuplicateProductScope_ReturnsConflict()
    {
        var tenantId = Guid.NewGuid();
        var ids = TestIds.Create();
        var dataContext = SeedPlanningData(tenantId, ids, availableQuantity: 3);
        var service = CreatePlanningService(dataContext, tenantId);

        var result = await service.CreateRuleAsync(new CreateInventoryReorderRuleRequest
        {
            ProductId = ids.ProductId,
            WarehouseId = ids.WarehouseId,
            LocationId = ids.LocationId,
            MinimumQuantity = 2,
            ReorderPoint = 4,
            ReorderQuantity = 8,
            IsActive = true
        });

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(409);
        dataContext.Added.OfType<InventoryReorderRule>().Should().BeEmpty();
        dataContext.SaveCount.Should().Be(0);
    }

    [Test]
    public async Task GetNearExpiryAsync_ExpiringLotWithStock_ReturnsReportRow()
    {
        var tenantId = Guid.NewGuid();
        var ids = TestIds.Create();
        var lotId = Guid.NewGuid();
        var dataContext = SeedPlanningData(tenantId, ids, availableQuantity: 3);
        dataContext.Set<InventoryLot>().Add(new InventoryLot
        {
            Id = lotId,
            TenantId = tenantId,
            ProductId = ids.ProductId,
            LotNumber = "LOT-EXP-1",
            ReceivedAt = DateTime.UtcNow.AddDays(-5),
            ExpiresAt = DateTime.UtcNow.AddDays(10),
            Status = InventoryLotStatus.Available,
            IsEnabled = true
        });
        dataContext.Set<StockBalance>().Add(new StockBalance
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProductId = ids.ProductId,
            WarehouseId = ids.WarehouseId,
            LocationId = ids.LocationId,
            LotId = lotId,
            OnHandQuantity = 4,
            ReservedQuantity = 1,
            AvailableQuantity = 3,
            IsEnabled = true
        });
        var planningService = CreatePlanningService(dataContext, tenantId);
        var reportingService = CreateReportingService(dataContext, tenantId, planningService);

        var result = await reportingService.GetNearExpiryAsync(new GetNearExpiryStockReportRequest
        {
            DaysAhead = 30
        });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Data.Should().ContainSingle();
        var row = result.Data![0];
        row.LotId.Should().Be(lotId);
        row.LotNumber.Should().Be("LOT-EXP-1");
        row.OnHandQuantity.Should().Be(4);
        row.AvailableQuantity.Should().Be(3);
        row.ProductName.Should().Be("Widget");
    }

    private static FakeDataContext SeedPlanningData(Guid tenantId, TestIds ids, decimal availableQuantity)
    {
        var dataContext = new FakeDataContext();
        dataContext.Set<Product>().Add(new Product
        {
            Id = ids.ProductId,
            TenantId = tenantId,
            Name = "Widget",
            CategoryId = Guid.NewGuid(),
            IsEnabled = true
        });
        dataContext.Set<Warehouse>().Add(new Warehouse
        {
            Id = ids.WarehouseId,
            TenantId = tenantId,
            Code = "MAIN",
            Name = "Main Warehouse",
            IsEnabled = true
        });
        dataContext.Set<InventoryLocation>().Add(new InventoryLocation
        {
            Id = ids.LocationId,
            TenantId = tenantId,
            WarehouseId = ids.WarehouseId,
            Code = "BIN-01",
            Name = "Bin 01",
            IsEnabled = true
        });
        dataContext.Set<StockBalance>().Add(new StockBalance
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProductId = ids.ProductId,
            WarehouseId = ids.WarehouseId,
            LocationId = ids.LocationId,
            OnHandQuantity = availableQuantity,
            ReservedQuantity = 0,
            AvailableQuantity = availableQuantity,
            IsEnabled = true
        });
        dataContext.Set<InventoryReorderRule>().Add(new InventoryReorderRule
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProductId = ids.ProductId,
            WarehouseId = ids.WarehouseId,
            LocationId = ids.LocationId,
            MinimumQuantity = 2,
            MaximumQuantity = 20,
            ReorderPoint = 5,
            ReorderQuantity = 6,
            PreferredSupplier = "Acme Supply",
            IsActive = true,
            IsEnabled = true
        });

        return dataContext;
    }

    private static InventoryPlanningService CreatePlanningService(FakeDataContext dataContext, Guid tenantId) =>
        new(dataContext, CreateHttpContextAccessor(tenantId));

    private static InventoryReportingService CreateReportingService(
        FakeDataContext dataContext,
        Guid tenantId,
        InventoryPlanningService planningService) =>
        new(dataContext, CreateHttpContextAccessor(tenantId), planningService);

    private static HttpContextAccessor CreateHttpContextAccessor(Guid tenantId) =>
        new()
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim("tenantId", tenantId.ToString())],
                    authenticationType: "Test"))
            }
        };

    private sealed record TestIds(Guid ProductId, Guid WarehouseId, Guid LocationId)
    {
        public static TestIds Create() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
    }

    private sealed class FakeDataContext : IDataContext
    {
        private readonly Dictionary<Type, IList> sets = [];

        public List<object> Added { get; } = [];
        public List<object> Updated { get; } = [];
        public int SaveCount { get; private set; }

        public List<T> Set<T>() where T : class
        {
            if (!sets.TryGetValue(typeof(T), out var set))
            {
                set = new List<T>();
                sets[typeof(T)] = set;
            }

            return (List<T>)set;
        }

        public IRemoteQuery<T> Query<T>() where T : class =>
            new InMemoryRemoteQuery<T>(Set<T>().AsQueryable());

        public void Add<T>(T entity) where T : class
        {
            Added.Add(entity);
            Set<T>().Add(entity);
        }

        public void Update<T>(T entity) where T : class =>
            Updated.Add(entity);

        public void Remove<T>(T entity) where T : class =>
            Set<T>().Remove(entity);

        public Task<DataContextResult> SaveChangesAsync(CancellationToken ct = default)
        {
            SaveCount++;
            return Task.FromResult(DataContextResult.Success());
        }
    }

    private sealed class InMemoryRemoteQuery<T>(IQueryable<T> queryable) : IRemoteQuery<T>
        where T : class
    {
        private IQueryable<T> query = queryable;

        public IRemoteQuery<T> Where(Expression<Func<T, bool>> predicate) { query = query.Where(predicate); return this; }
        public IRemoteQuery<T> OrderBy<TKey>(Expression<Func<T, TKey>> keySelector) { query = query.OrderBy(keySelector); return this; }
        public IRemoteQuery<T> OrderByDescending<TKey>(Expression<Func<T, TKey>> keySelector) { query = query.OrderByDescending(keySelector); return this; }
        public IRemoteQuery<T> ThenBy<TKey>(Expression<Func<T, TKey>> keySelector) { query = ((IOrderedQueryable<T>)query).ThenBy(keySelector); return this; }
        public IRemoteQuery<T> ThenByDescending<TKey>(Expression<Func<T, TKey>> keySelector) { query = ((IOrderedQueryable<T>)query).ThenByDescending(keySelector); return this; }
        public IRemoteQuery<T> Skip(int count) { query = query.Skip(count); return this; }
        public IRemoteQuery<T> Take(int count) { query = query.Take(count); return this; }
        public IRemoteQuery<T> Include<TProperty>(Expression<Func<T, TProperty>> navigationSelector) => this;
        public IRemoteQuery<T> Distinct() { query = query.Distinct(); return this; }
        public IRemoteQuery<T> DistinctBy<TKey>(Expression<Func<T, TKey>> keySelector) { query = query.AsEnumerable().DistinctBy(keySelector.Compile()).AsQueryable(); return this; }
        public IRemoteQuery<T> NoCache() => this;
        public IRemoteQuery<T> IgnoreQueryFilters() => this;
        public Task<List<T>> ToListAsync(CancellationToken ct = default) => Task.FromResult(query.ToList());
        public Task<T?> FirstOrDefaultAsync(CancellationToken ct = default) => Task.FromResult(query.FirstOrDefault());
        public Task<T?> SingleOrDefaultAsync(CancellationToken ct = default) => Task.FromResult(query.SingleOrDefault());
        public async IAsyncEnumerable<T> ToAsyncEnumerable(int chunkSize = 100, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            foreach (var item in query)
            {
                ct.ThrowIfCancellationRequested();
                yield return item;
                await Task.Yield();
            }
        }
        public Task<int> CountAsync(CancellationToken ct = default) => Task.FromResult(query.Count());
        public Task<bool> AnyAsync(CancellationToken ct = default) => Task.FromResult(query.Any());
        public Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default) => Task.FromResult(query.Any(predicate));
        public Task<bool> AllAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default) => Task.FromResult(query.All(predicate));
        public Task<TResult?> MinAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken ct = default) => Task.FromResult(query.Select(selector).Min());
        public Task<TResult?> MaxAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken ct = default) => Task.FromResult(query.Select(selector).Max());
        public Task<T?> MinByAsync<TKey>(Expression<Func<T, TKey>> keySelector, CancellationToken ct = default) => Task.FromResult(query.AsEnumerable().MinBy(keySelector.Compile()));
        public Task<T?> MaxByAsync<TKey>(Expression<Func<T, TKey>> keySelector, CancellationToken ct = default) => Task.FromResult(query.AsEnumerable().MaxBy(keySelector.Compile()));
        public Task<decimal> SumAsync(Expression<Func<T, decimal>> selector, CancellationToken ct = default) => Task.FromResult(query.Sum(selector));
        public Task<double> AverageAsync(Expression<Func<T, decimal>> selector, CancellationToken ct = default) => Task.FromResult((double)query.Average(selector));
        public Task<List<GroupResult<TKey, T>>> GroupByAsync<TKey>(Expression<Func<T, TKey>> keySelector, CancellationToken ct = default)
        {
            var compiled = keySelector.Compile();
            return Task.FromResult(query.AsEnumerable()
                .GroupBy(compiled)
                .Select(g => new GroupResult<TKey, T> { Key = g.Key, Items = g.ToList() })
                .ToList());
        }
    }
}
