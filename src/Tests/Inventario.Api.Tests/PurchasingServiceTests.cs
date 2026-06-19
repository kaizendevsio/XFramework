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
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Purchasing;
using XFramework.Inventario.Domain.Shared.Enums;

namespace Inventario.Api.Tests;

[TestFixture]
public sealed class PurchasingServiceTests
{
    [Test]
    public async Task ReceiveAsync_OpenPurchaseOrderLine_PostsReceiptAndMarksOrderPartiallyReceived()
    {
        var tenantId = Guid.NewGuid();
        var ids = TestIds.Create();
        var dataContext = SeedPurchasingData(tenantId, ids, orderedQuantity: 10);
        var service = CreateService(dataContext, tenantId);

        var result = await service.ReceiveAsync(new ReceiveInventoryRequest
        {
            PurchaseOrderId = ids.PurchaseOrderId,
            WarehouseId = ids.WarehouseId,
            LocationId = ids.LocationId,
            IdempotencyKey = "receive-po-1",
            Lines =
            [
                new()
                {
                    PurchaseOrderLineId = ids.PurchaseOrderLineId,
                    ProductId = ids.ProductId,
                    Quantity = 4,
                    UnitCost = 2.50m,
                    LotNumber = "LOT-RCV-1"
                }
            ]
        });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.StatusCode.Should().Be(201);
        result.Data!.Lines.Should().ContainSingle();

        dataContext.Set<PurchaseOrder>().Single().Status.Should().Be(PurchaseOrderStatus.PartiallyReceived);
        dataContext.Set<PurchaseOrderLine>().Single().ReceivedQuantity.Should().Be(4);
        dataContext.Set<InventoryLot>().Should().ContainSingle(x => x.LotNumber == "LOT-RCV-1");
        dataContext.Set<StockBalance>().Should().ContainSingle(x => x.LotId != null && x.OnHandQuantity == 4 && x.AvailableQuantity == 4);
        dataContext.Added.OfType<InventoryMovement>().Should().ContainSingle(x =>
            x.MovementType == InventoryMovementType.Receipt &&
            x.QuantityDelta == 4 &&
            x.IdempotencyKey == "receive-po-1:line:0");
        dataContext.SaveCount.Should().Be(1);
    }

    [Test]
    public async Task ReceiveAsync_OverRemainingPurchaseOrderQuantity_ReturnsConflictWithoutSaving()
    {
        var tenantId = Guid.NewGuid();
        var ids = TestIds.Create();
        var dataContext = SeedPurchasingData(tenantId, ids, orderedQuantity: 10, receivedQuantity: 8);
        var service = CreateService(dataContext, tenantId);

        var result = await service.ReceiveAsync(new ReceiveInventoryRequest
        {
            PurchaseOrderId = ids.PurchaseOrderId,
            WarehouseId = ids.WarehouseId,
            LocationId = ids.LocationId,
            Lines =
            [
                new()
                {
                    PurchaseOrderLineId = ids.PurchaseOrderLineId,
                    ProductId = ids.ProductId,
                    Quantity = 3
                }
            ]
        });

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(409);
        dataContext.Set<PurchaseOrderLine>().Single().ReceivedQuantity.Should().Be(8);
        dataContext.Added.OfType<ReceivingDocument>().Should().ContainSingle();
        dataContext.Added.OfType<InventoryMovement>().Should().BeEmpty();
        dataContext.SaveCount.Should().Be(0);
    }

    [Test]
    public async Task ReceiveAsync_SameIdempotencyKeyAndPayload_ReplaysWithoutDoublePosting()
    {
        var tenantId = Guid.NewGuid();
        var ids = TestIds.Create();
        var dataContext = SeedPurchasingData(tenantId, ids, orderedQuantity: 10);
        var service = CreateService(dataContext, tenantId);
        var request = new ReceiveInventoryRequest
        {
            ReceiptNumber = "RCV-100",
            WarehouseId = ids.WarehouseId,
            LocationId = ids.LocationId,
            IdempotencyKey = "direct-receive-1",
            Lines =
            [
                new()
                {
                    ProductId = ids.ProductId,
                    Quantity = 5
                }
            ]
        };

        var first = await service.ReceiveAsync(request);
        var replay = await service.ReceiveAsync(request);

        first.IsSuccess.Should().BeTrue(first.Message);
        replay.IsSuccess.Should().BeTrue(replay.Message);
        replay.Message.Should().Be("Receiving document already processed.");
        dataContext.Added.OfType<ReceivingDocument>().Should().ContainSingle(x => x.IdempotencyKey == "direct-receive-1");
        dataContext.Added.OfType<InventoryMovement>().Should().ContainSingle(x => x.IdempotencyKey == "direct-receive-1:line:0");
        dataContext.Set<StockBalance>().Should().ContainSingle(x => x.OnHandQuantity == 5);
        dataContext.SaveCount.Should().Be(1);
    }

    [Test]
    public async Task ReceiveAsync_SameIdempotencyKeyWithDifferentPayload_ReturnsConflict()
    {
        var tenantId = Guid.NewGuid();
        var ids = TestIds.Create();
        var dataContext = SeedPurchasingData(tenantId, ids, orderedQuantity: 10);
        var service = CreateService(dataContext, tenantId);

        var first = await service.ReceiveAsync(new ReceiveInventoryRequest
        {
            ReceiptNumber = "RCV-100",
            WarehouseId = ids.WarehouseId,
            LocationId = ids.LocationId,
            IdempotencyKey = "direct-receive-1",
            Lines = [new() { ProductId = ids.ProductId, Quantity = 5 }]
        });
        var conflict = await service.ReceiveAsync(new ReceiveInventoryRequest
        {
            ReceiptNumber = "RCV-100",
            WarehouseId = ids.WarehouseId,
            LocationId = ids.LocationId,
            IdempotencyKey = "direct-receive-1",
            Lines = [new() { ProductId = ids.ProductId, Quantity = 6 }]
        });

        first.IsSuccess.Should().BeTrue(first.Message);
        conflict.IsSuccess.Should().BeFalse();
        conflict.StatusCode.Should().Be(409);
        dataContext.SaveCount.Should().Be(1);
    }

    private static FakeDataContext SeedPurchasingData(
        Guid tenantId,
        TestIds ids,
        decimal orderedQuantity,
        decimal receivedQuantity = 0)
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
        dataContext.Set<PurchaseOrder>().Add(new PurchaseOrder
        {
            Id = ids.PurchaseOrderId,
            TenantId = tenantId,
            OrderNumber = "PO-100",
            Status = PurchaseOrderStatus.Open,
            OrderDate = DateTime.UtcNow.AddDays(-1),
            IsEnabled = true
        });
        dataContext.Set<PurchaseOrderLine>().Add(new PurchaseOrderLine
        {
            Id = ids.PurchaseOrderLineId,
            TenantId = tenantId,
            PurchaseOrderId = ids.PurchaseOrderId,
            ProductId = ids.ProductId,
            OrderedQuantity = orderedQuantity,
            ReceivedQuantity = receivedQuantity,
            UnitCost = 2.50m,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        });

        return dataContext;
    }

    private static PurchasingService CreateService(FakeDataContext dataContext, Guid tenantId)
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
        var stockPostingService = new StockPostingService(dataContext, httpContextAccessor);
        return new PurchasingService(dataContext, httpContextAccessor, stockPostingService);
    }

    private sealed record TestIds(
        Guid ProductId,
        Guid WarehouseId,
        Guid LocationId,
        Guid PurchaseOrderId,
        Guid PurchaseOrderLineId)
    {
        public static TestIds Create() => new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid());
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
