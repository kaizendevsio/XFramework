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
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Stock;
using XFramework.Inventario.Domain.Shared.Enums;

namespace Inventario.Api.Tests;

[TestFixture]
public sealed class StockPostingServiceTests
{
    [Test]
    public async Task PostAsync_OpeningBalance_CreatesBalanceMovementAndUpdatesProductSnapshot()
    {
        var tenantId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var dataContext = SeedStockData(tenantId, productId, warehouseId, locationId);
        var service = CreateService(dataContext, tenantId);

        var result = await service.PostAsync(new PostStockMovementRequest
        {
            ProductId = productId,
            WarehouseId = warehouseId,
            LocationId = locationId,
            MovementType = InventoryMovementType.OpeningBalance,
            Quantity = 25,
            Reason = "Initial count"
        });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Data!.OnHandQuantity.Should().Be(25);
        result.Data.ReservedQuantity.Should().Be(0);
        result.Data.AvailableQuantity.Should().Be(25);

        dataContext.Set<StockBalance>().Should().ContainSingle(x =>
            x.ProductId == productId &&
            x.OnHandQuantity == 25 &&
            x.AvailableQuantity == 25);
        dataContext.Added.OfType<InventoryMovement>().Should().ContainSingle(x =>
            x.ProductId == productId &&
            x.MovementType == InventoryMovementType.OpeningBalance &&
            x.QuantityDelta == 25);
        dataContext.Set<Product>().Single().StockQuantity.Should().Be(25);
        dataContext.SaveCount.Should().Be(1);
    }

    [Test]
    public async Task PostAsync_ShipmentBeyondAvailableStock_ReturnsConflictWithoutSaving()
    {
        var tenantId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var dataContext = SeedStockData(tenantId, productId, warehouseId, locationId);
        dataContext.Set<StockBalance>().Add(new StockBalance
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProductId = productId,
            WarehouseId = warehouseId,
            LocationId = locationId,
            OnHandQuantity = 3,
            ReservedQuantity = 0,
            AvailableQuantity = 3
        });
        var service = CreateService(dataContext, tenantId);

        var result = await service.PostAsync(new PostStockMovementRequest
        {
            ProductId = productId,
            WarehouseId = warehouseId,
            LocationId = locationId,
            MovementType = InventoryMovementType.Shipment,
            Quantity = 5
        });

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(409);
        dataContext.Added.OfType<InventoryMovement>().Should().BeEmpty();
        dataContext.SaveCount.Should().Be(0);
    }

    [Test]
    public async Task PostAsync_SameProductLocationWithDifferentLots_CreatesSeparateBalances()
    {
        var tenantId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var firstLotId = Guid.NewGuid();
        var secondLotId = Guid.NewGuid();
        var dataContext = SeedStockData(tenantId, productId, warehouseId, locationId);
        dataContext.Set<InventoryLot>().AddRange([
            CreateLot(tenantId, productId, firstLotId, "LOT-A"),
            CreateLot(tenantId, productId, secondLotId, "LOT-B")
        ]);
        var service = CreateService(dataContext, tenantId);

        var firstResult = await service.PostAsync(new PostStockMovementRequest
        {
            ProductId = productId,
            WarehouseId = warehouseId,
            LocationId = locationId,
            LotId = firstLotId,
            MovementType = InventoryMovementType.OpeningBalance,
            Quantity = 10
        });
        var secondResult = await service.PostAsync(new PostStockMovementRequest
        {
            ProductId = productId,
            WarehouseId = warehouseId,
            LocationId = locationId,
            LotId = secondLotId,
            MovementType = InventoryMovementType.OpeningBalance,
            Quantity = 5
        });

        firstResult.IsSuccess.Should().BeTrue(firstResult.Message);
        secondResult.IsSuccess.Should().BeTrue(secondResult.Message);
        dataContext.Set<StockBalance>().Should().HaveCount(2);
        dataContext.Set<StockBalance>().Should().ContainSingle(x => x.LotId == firstLotId && x.OnHandQuantity == 10);
        dataContext.Set<StockBalance>().Should().ContainSingle(x => x.LotId == secondLotId && x.OnHandQuantity == 5);
        dataContext.Added.OfType<InventoryMovement>().Should().ContainSingle(x => x.LotId == firstLotId && x.QuantityDelta == 10);
        dataContext.Added.OfType<InventoryMovement>().Should().ContainSingle(x => x.LotId == secondLotId && x.QuantityDelta == 5);
        dataContext.Set<Product>().Single().StockQuantity.Should().Be(15);
    }

    [Test]
    public async Task PostAsync_LotForDifferentProduct_ReturnsBadRequestWithoutSaving()
    {
        var tenantId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var lotId = Guid.NewGuid();
        var dataContext = SeedStockData(tenantId, productId, warehouseId, locationId);
        dataContext.Set<InventoryLot>().Add(CreateLot(tenantId, Guid.NewGuid(), lotId, "OTHER-PRODUCT"));
        var service = CreateService(dataContext, tenantId);

        var result = await service.PostAsync(new PostStockMovementRequest
        {
            ProductId = productId,
            WarehouseId = warehouseId,
            LocationId = locationId,
            LotId = lotId,
            MovementType = InventoryMovementType.OpeningBalance,
            Quantity = 10
        });

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        dataContext.Set<StockBalance>().Should().BeEmpty();
        dataContext.SaveCount.Should().Be(0);
    }

    [Test]
    public async Task PostAsync_SameIdempotencyKeyAndPayload_ReplaysWithoutDoublePosting()
    {
        var tenantId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var dataContext = SeedStockData(tenantId, productId, warehouseId, locationId);
        var service = CreateService(dataContext, tenantId);
        var request = new PostStockMovementRequest
        {
            ProductId = productId,
            WarehouseId = warehouseId,
            LocationId = locationId,
            MovementType = InventoryMovementType.OpeningBalance,
            Quantity = 25,
            Reason = "Initial count",
            IdempotencyKey = "stock-open-1"
        };

        var firstResult = await service.PostAsync(request);
        var replayResult = await service.PostAsync(request);

        firstResult.IsSuccess.Should().BeTrue(firstResult.Message);
        replayResult.IsSuccess.Should().BeTrue(replayResult.Message);
        replayResult.Data!.IsIdempotentReplay.Should().BeTrue();
        replayResult.Data.IdempotencyKey.Should().Be("stock-open-1");
        replayResult.Data.OnHandQuantity.Should().Be(25);
        dataContext.Set<StockBalance>().Should().ContainSingle(x => x.OnHandQuantity == 25);
        dataContext.Added.OfType<InventoryMovement>().Should().ContainSingle(x => x.IdempotencyKey == "stock-open-1");
        dataContext.Set<Product>().Single().StockQuantity.Should().Be(25);
        dataContext.SaveCount.Should().Be(1);
    }

    [Test]
    public async Task PostAsync_SameIdempotencyKeyWithDifferentPayload_ReturnsConflict()
    {
        var tenantId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var dataContext = SeedStockData(tenantId, productId, warehouseId, locationId);
        var service = CreateService(dataContext, tenantId);

        var firstResult = await service.PostAsync(new PostStockMovementRequest
        {
            ProductId = productId,
            WarehouseId = warehouseId,
            LocationId = locationId,
            MovementType = InventoryMovementType.OpeningBalance,
            Quantity = 25,
            IdempotencyKey = "stock-open-1"
        });
        var conflictResult = await service.PostAsync(new PostStockMovementRequest
        {
            ProductId = productId,
            WarehouseId = warehouseId,
            LocationId = locationId,
            MovementType = InventoryMovementType.OpeningBalance,
            Quantity = 30,
            IdempotencyKey = "stock-open-1"
        });

        firstResult.IsSuccess.Should().BeTrue(firstResult.Message);
        conflictResult.IsSuccess.Should().BeFalse();
        conflictResult.StatusCode.Should().Be(409);
        dataContext.Set<StockBalance>().Should().ContainSingle(x => x.OnHandQuantity == 25);
        dataContext.Added.OfType<InventoryMovement>().Should().ContainSingle(x => x.IdempotencyKey == "stock-open-1");
        dataContext.SaveCount.Should().Be(1);
    }

    private static FakeDataContext SeedStockData(Guid tenantId, Guid productId, Guid warehouseId, Guid locationId)
    {
        var dataContext = new FakeDataContext();
        dataContext.Set<Product>().Add(new Product
        {
            Id = productId,
            TenantId = tenantId,
            Name = "Widget",
            CategoryId = Guid.NewGuid(),
            IsEnabled = true
        });
        dataContext.Set<Warehouse>().Add(new Warehouse
        {
            Id = warehouseId,
            TenantId = tenantId,
            Code = "MAIN",
            Name = "Main Warehouse",
            IsEnabled = true
        });
        dataContext.Set<InventoryLocation>().Add(new InventoryLocation
        {
            Id = locationId,
            TenantId = tenantId,
            WarehouseId = warehouseId,
            Code = "BIN-01",
            Name = "Bin 01",
            IsEnabled = true
        });

        return dataContext;
    }

    private static InventoryLot CreateLot(Guid tenantId, Guid productId, Guid lotId, string lotNumber) =>
        new()
        {
            Id = lotId,
            TenantId = tenantId,
            ProductId = productId,
            LotNumber = lotNumber,
            ReceivedAt = DateTime.UtcNow,
            Status = InventoryLotStatus.Available,
            IsEnabled = true
        };

    private static StockPostingService CreateService(FakeDataContext dataContext, Guid tenantId)
    {
        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim("tenantId", tenantId.ToString())],
                    authenticationType: "Test"))
            }
        };

        return new StockPostingService(dataContext, httpContextAccessor);
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
