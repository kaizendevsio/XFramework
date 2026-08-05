using System.Collections;
using System.Linq.Expressions;
using System.Security.Claims;
using IdentityServer.Domain.Shared.Contracts;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using XFramework.Core.Patterns;
using XFramework.Core.Services.FeatureGates;
using XFramework.Domain.Shared.DataContext;
using XFramework.Inventario.Api.Services;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Reservations;
using XFramework.Inventario.Domain.Shared.Enums;

namespace Inventario.Api.Tests;

[TestFixture]
public sealed class ReservationServiceTests
{
    [Test]
    public async Task ReserveAsync_AvailableStock_CreatesReservationAndUpdatesReservedBalance()
    {
        var tenantId = Guid.NewGuid();
        var ids = TestIds.Create();
        var dataContext = SeedReservationData(tenantId, ids, onHand: 10, reserved: 0);
        var service = CreateService(dataContext, tenantId);

        var result = await service.ReserveAsync(new ReserveInventoryRequest
        {
            ProductId = ids.ProductId,
            WarehouseId = ids.WarehouseId,
            LocationId = ids.LocationId,
            Quantity = 4,
            ReferenceType = "order"
        });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.StatusCode.Should().Be(201);
        result.Data!.Status.Should().Be(ReservationStatus.Active);

        var balance = dataContext.Set<StockBalance>().Single();
        balance.OnHandQuantity.Should().Be(10);
        balance.ReservedQuantity.Should().Be(4);
        balance.AvailableQuantity.Should().Be(6);

        dataContext.Added.OfType<Reservation>().Should().ContainSingle(x =>
            x.ProductId == ids.ProductId &&
            x.Quantity == 4 &&
            x.ReferenceType == "order");
        dataContext.Added.OfType<InventoryMovement>().Should().ContainSingle(x =>
            x.MovementType == InventoryMovementType.Reservation &&
            x.QuantityDelta == 4);
        dataContext.SaveCount.Should().Be(1);
    }

    [Test]
    public async Task ReserveAsync_SameIdempotencyKey_ReplaysExistingReservationWithoutDoubleReserving()
    {
        var tenantId = Guid.NewGuid();
        var ids = TestIds.Create();
        var dataContext = SeedReservationData(tenantId, ids, onHand: 10, reserved: 0);
        var service = CreateService(dataContext, tenantId);
        var request = new ReserveInventoryRequest
        {
            ProductId = ids.ProductId,
            WarehouseId = ids.WarehouseId,
            LocationId = ids.LocationId,
            Quantity = 4,
            ReferenceType = "POS.SaleLine",
            ReferenceId = Guid.NewGuid(),
            IdempotencyKey = "pos-line-reservation"
        };

        var first = await service.ReserveAsync(request);
        var second = await service.ReserveAsync(request);

        first.IsSuccess.Should().BeTrue(first.Message);
        second.IsSuccess.Should().BeTrue(second.Message);
        second.StatusCode.Should().Be(200);
        second.Data!.Id.Should().Be(first.Data!.Id);
        second.Data.IdempotencyKey.Should().Be("pos-line-reservation");
        dataContext.Set<StockBalance>().Single().ReservedQuantity.Should().Be(4);
        dataContext.Added.OfType<Reservation>().Should().ContainSingle();
        dataContext.SaveCount.Should().Be(1);
    }

    [Test]
    public async Task ReserveAsync_SameIdempotencyKeyDifferentPayload_ReturnsConflict()
    {
        var tenantId = Guid.NewGuid();
        var ids = TestIds.Create();
        var dataContext = SeedReservationData(tenantId, ids, onHand: 10, reserved: 0);
        var service = CreateService(dataContext, tenantId);

        await service.ReserveAsync(new ReserveInventoryRequest
        {
            ProductId = ids.ProductId,
            WarehouseId = ids.WarehouseId,
            LocationId = ids.LocationId,
            Quantity = 4,
            IdempotencyKey = "pos-line-reservation"
        });
        var conflict = await service.ReserveAsync(new ReserveInventoryRequest
        {
            ProductId = ids.ProductId,
            WarehouseId = ids.WarehouseId,
            LocationId = ids.LocationId,
            Quantity = 5,
            IdempotencyKey = "pos-line-reservation"
        });

        conflict.IsSuccess.Should().BeFalse();
        conflict.StatusCode.Should().Be(409);
        dataContext.Set<StockBalance>().Single().ReservedQuantity.Should().Be(4);
        dataContext.SaveCount.Should().Be(1);
    }

    [Test]
    public async Task ReserveAsync_MultipleLots_AllocatesEarliestExpiryFirst()
    {
        var tenantId = Guid.NewGuid();
        var ids = TestIds.Create();
        var firstLotId = Guid.NewGuid();
        var secondLotId = Guid.NewGuid();
        var dataContext = SeedReservationData(tenantId, ids, onHand: 0, reserved: 0, includeDefaultBalance: false);
        AddLotBalance(dataContext, tenantId, ids, firstLotId, "LOT-EXP-2", DateTime.UtcNow.AddDays(20), 5);
        AddLotBalance(dataContext, tenantId, ids, secondLotId, "LOT-EXP-1", DateTime.UtcNow.AddDays(5), 4);
        var service = CreateService(dataContext, tenantId);

        var result = await service.ReserveAsync(new ReserveInventoryRequest
        {
            ProductId = ids.ProductId,
            WarehouseId = ids.WarehouseId,
            LocationId = ids.LocationId,
            Quantity = 7
        });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Data!.Allocations.Should().HaveCount(2);
        result.Data.Allocations[0].LotId.Should().Be(secondLotId);
        result.Data.Allocations[0].Quantity.Should().Be(4);
        result.Data.Allocations[1].LotId.Should().Be(firstLotId);
        result.Data.Allocations[1].Quantity.Should().Be(3);

        dataContext.Set<StockBalance>().Single(x => x.LotId == secondLotId).ReservedQuantity.Should().Be(4);
        dataContext.Set<StockBalance>().Single(x => x.LotId == firstLotId).ReservedQuantity.Should().Be(3);
        dataContext.Added.OfType<InventoryMovement>()
            .Where(x => x.MovementType == InventoryMovementType.Reservation)
            .Should().HaveCount(2);
        dataContext.SaveCount.Should().Be(1);
    }

    [Test]
    public async Task ReserveAsync_ExpiredLots_AreExcludedByDefault()
    {
        var tenantId = Guid.NewGuid();
        var ids = TestIds.Create();
        var expiredLotId = Guid.NewGuid();
        var dataContext = SeedReservationData(tenantId, ids, onHand: 0, reserved: 0, includeDefaultBalance: false);
        AddLotBalance(dataContext, tenantId, ids, expiredLotId, "LOT-OLD", DateTime.UtcNow.AddDays(-1), 5);
        var service = CreateService(dataContext, tenantId);

        var result = await service.ReserveAsync(new ReserveInventoryRequest
        {
            ProductId = ids.ProductId,
            WarehouseId = ids.WarehouseId,
            LocationId = ids.LocationId,
            Quantity = 1
        });

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(409);
        dataContext.Set<StockBalance>().Single().ReservedQuantity.Should().Be(0);
        dataContext.Added.OfType<ReservationAllocation>().Should().BeEmpty();
        dataContext.SaveCount.Should().Be(0);
    }

    [Test]
    public async Task ReserveAsync_ExpiredLotOverride_WithAdminClaim_AllowsAllocation()
    {
        var tenantId = Guid.NewGuid();
        var ids = TestIds.Create();
        var expiredLotId = Guid.NewGuid();
        var dataContext = SeedReservationData(tenantId, ids, onHand: 0, reserved: 0, includeDefaultBalance: false);
        AddLotBalance(dataContext, tenantId, ids, expiredLotId, "LOT-OLD", DateTime.UtcNow.AddDays(-1), 5);
        var service = CreateService(dataContext, tenantId, includeAdminClaim: true);

        var result = await service.ReserveAsync(new ReserveInventoryRequest
        {
            ProductId = ids.ProductId,
            WarehouseId = ids.WarehouseId,
            LocationId = ids.LocationId,
            LotId = expiredLotId,
            Quantity = 2,
            AllowExpiredLotOverride = true,
            ExpiredLotOverrideReason = "QA hold released by supervisor"
        });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Data!.Allocations.Should().ContainSingle(x =>
            x.LotId == expiredLotId &&
            x.Quantity == 2 &&
            x.ExpiredLotOverrideReason == "QA hold released by supervisor");
        dataContext.Set<StockBalance>().Single().ReservedQuantity.Should().Be(2);
        dataContext.SaveCount.Should().Be(1);
    }

    [Test]
    public async Task ReserveAsync_BeyondAvailableStock_ReturnsConflictWithoutSaving()
    {
        var tenantId = Guid.NewGuid();
        var ids = TestIds.Create();
        var dataContext = SeedReservationData(tenantId, ids, onHand: 2, reserved: 0);
        var service = CreateService(dataContext, tenantId);

        var result = await service.ReserveAsync(new ReserveInventoryRequest
        {
            ProductId = ids.ProductId,
            WarehouseId = ids.WarehouseId,
            LocationId = ids.LocationId,
            Quantity = 3
        });

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(409);
        dataContext.Added.OfType<Reservation>().Should().BeEmpty();
        dataContext.Added.OfType<InventoryMovement>().Should().BeEmpty();
        dataContext.SaveCount.Should().Be(0);
    }

    [Test]
    public async Task FulfillAsync_ActiveReservation_ReleasesReservationAndShipsStock()
    {
        var tenantId = Guid.NewGuid();
        var ids = TestIds.Create();
        var reservationId = Guid.NewGuid();
        var dataContext = SeedReservationData(tenantId, ids, onHand: 10, reserved: 4, reservationId);
        var service = CreateService(dataContext, tenantId);

        var result = await service.FulfillAsync(new FulfillReservationRequest
        {
            ReservationId = reservationId
        });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Data!.Status.Should().Be(ReservationStatus.Fulfilled);
        result.Data.FulfilledAt.Should().NotBeNull();

        var balance = dataContext.Set<StockBalance>().Single();
        balance.OnHandQuantity.Should().Be(6);
        balance.ReservedQuantity.Should().Be(0);
        balance.AvailableQuantity.Should().Be(6);

        dataContext.Added.OfType<InventoryMovement>().Should().ContainSingle(x =>
            x.MovementType == InventoryMovementType.Release &&
            x.QuantityDelta == -4);
        dataContext.Added.OfType<InventoryMovement>().Should().ContainSingle(x =>
            x.MovementType == InventoryMovementType.Shipment &&
            x.QuantityDelta == -4);
        dataContext.Set<ReservationAllocation>().Single().Status.Should().Be(ReservationAllocationStatus.Fulfilled);
        dataContext.Set<Product>().Single().StockQuantity.Should().Be(6);
        dataContext.SaveCount.Should().Be(1);
    }

    [Test]
    public async Task FulfillAsync_AlreadyFulfilledReservation_ReplaysWithoutPostingStockAgain()
    {
        var tenantId = Guid.NewGuid();
        var ids = TestIds.Create();
        var reservationId = Guid.NewGuid();
        var dataContext = SeedReservationData(tenantId, ids, onHand: 10, reserved: 4, reservationId);
        var service = CreateService(dataContext, tenantId);
        var request = new FulfillReservationRequest { ReservationId = reservationId };

        var first = await service.FulfillAsync(request);
        var second = await service.FulfillAsync(request);

        first.IsSuccess.Should().BeTrue(first.Message);
        second.IsSuccess.Should().BeTrue(second.Message);
        second.Data!.Status.Should().Be(ReservationStatus.Fulfilled);
        dataContext.Added.OfType<InventoryMovement>().Should().HaveCount(2);
        dataContext.SaveCount.Should().Be(1);
    }

    [Test]
    public async Task ReleaseAsync_AlreadyReleasedReservation_ReplaysWithoutPostingStockAgain()
    {
        var tenantId = Guid.NewGuid();
        var ids = TestIds.Create();
        var reservationId = Guid.NewGuid();
        var dataContext = SeedReservationData(tenantId, ids, onHand: 10, reserved: 4, reservationId);
        var service = CreateService(dataContext, tenantId);
        var request = new ReleaseReservationRequest { ReservationId = reservationId };

        var first = await service.ReleaseAsync(request);
        var second = await service.ReleaseAsync(request);

        first.IsSuccess.Should().BeTrue(first.Message);
        second.IsSuccess.Should().BeTrue(second.Message);
        second.Data!.Status.Should().Be(ReservationStatus.Released);
        dataContext.Added.OfType<InventoryMovement>().Should().ContainSingle();
        dataContext.SaveCount.Should().Be(1);
    }

    [Test]
    public async Task ExpireAsync_DueReservation_ReleasesReservedQuantity()
    {
        var tenantId = Guid.NewGuid();
        var ids = TestIds.Create();
        var reservationId = Guid.NewGuid();
        var dataContext = SeedReservationData(
            tenantId,
            ids,
            onHand: 5,
            reserved: 2,
            reservationId,
            expiresAt: DateTime.UtcNow.AddMinutes(-5));
        var service = CreateService(dataContext, tenantId);

        var result = await service.ExpireAsync(new ExpireReservationsRequest
        {
            ExpiresBefore = DateTime.UtcNow
        });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Data.Should().Be(1);

        var reservation = dataContext.Set<Reservation>().Single();
        reservation.Status.Should().Be(ReservationStatus.Expired);
        reservation.ReleasedAt.Should().NotBeNull();

        var balance = dataContext.Set<StockBalance>().Single();
        balance.OnHandQuantity.Should().Be(5);
        balance.ReservedQuantity.Should().Be(0);
        balance.AvailableQuantity.Should().Be(5);
        dataContext.Added.OfType<InventoryMovement>().Should().ContainSingle(x =>
            x.MovementType == InventoryMovementType.Release &&
            x.QuantityDelta == -2);
        dataContext.SaveCount.Should().Be(1);
    }

    private static FakeDataContext SeedReservationData(
        Guid tenantId,
        TestIds ids,
        decimal onHand,
        decimal reserved,
        Guid? reservationId = null,
        DateTime? expiresAt = null,
        bool includeDefaultBalance = true)
    {
        var dataContext = new FakeDataContext();
        dataContext.Set<Product>().Add(new Product
        {
            Id = ids.ProductId,
            TenantId = tenantId,
            Name = "Widget",
            CategoryId = Guid.NewGuid(),
            StockQuantity = (int)onHand,
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
        if (includeDefaultBalance)
        {
            dataContext.Set<StockBalance>().Add(new StockBalance
            {
                Id = ids.BalanceId,
                TenantId = tenantId,
                ProductId = ids.ProductId,
                WarehouseId = ids.WarehouseId,
                LocationId = ids.LocationId,
                OnHandQuantity = onHand,
                ReservedQuantity = reserved,
                AvailableQuantity = onHand - reserved,
                IsEnabled = true
            });
        }

        if (reservationId is { } id)
        {
            dataContext.Set<Reservation>().Add(new Reservation
            {
                Id = id,
                TenantId = tenantId,
                ProductId = ids.ProductId,
                WarehouseId = ids.WarehouseId,
                LocationId = ids.LocationId,
                StockBalanceId = ids.BalanceId,
                Quantity = reserved,
                Status = ReservationStatus.Active,
                ReservedAt = DateTime.UtcNow.AddMinutes(-10),
                ExpiresAt = expiresAt,
                IsEnabled = true
            });
        }

        return dataContext;
    }

    private static void AddLotBalance(
        FakeDataContext dataContext,
        Guid tenantId,
        TestIds ids,
        Guid lotId,
        string lotNumber,
        DateTime expiresAt,
        decimal onHand)
    {
        dataContext.Set<InventoryLot>().Add(new InventoryLot
        {
            Id = lotId,
            TenantId = tenantId,
            ProductId = ids.ProductId,
            LotNumber = lotNumber,
            ReceivedAt = DateTime.UtcNow.AddDays(-10),
            ExpiresAt = expiresAt,
            Status = expiresAt <= DateTime.UtcNow ? InventoryLotStatus.Expired : InventoryLotStatus.Available,
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
            OnHandQuantity = onHand,
            ReservedQuantity = 0,
            AvailableQuantity = onHand,
            IsEnabled = true
        });
    }

    private static ReservationService CreateService(
        FakeDataContext dataContext,
        Guid tenantId,
        bool includeAdminClaim = false)
    {
        var roles = includeAdminClaim
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Admin" }
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var invocationContext = new TestTrustedInvocationContextAccessor(tenantId, roles);

        var featureService = new FakeTenantModuleFeatureService();
        var productVariationService = new ProductVariationService(dataContext, invocationContext, featureService);
        var stockPostingService = new StockPostingService(
            dataContext,
            invocationContext,
            featureService,
            productVariationService);
        var allocationService = new InventoryAllocationService(dataContext, invocationContext, stockPostingService);
        return new ReservationService(dataContext, invocationContext, allocationService);
    }

    private sealed class FakeTenantModuleFeatureService : ITenantModuleFeatureService
    {
        public Task<Result<bool>> IsEnabledAsync(
            Guid tenantId,
            string moduleKey,
            string? subFeatureKey = null,
            CancellationToken ct = default) =>
            Task.FromResult(Result<bool>.Success(false));

        public Task<Result> EnsureEnabledAsync(
            Guid tenantId,
            string moduleKey,
            string? subFeatureKey = null,
            CancellationToken ct = default) =>
            Task.FromResult(Result.Forbidden($"Feature disabled: '{TenantModuleFeatureKeys.Combine(moduleKey, subFeatureKey)}' is not enabled for this tenant."));

        public void Invalidate(Guid tenantId, string moduleKey, string? subFeatureKey = null)
        {
        }
    }

    private sealed record TestIds(Guid ProductId, Guid WarehouseId, Guid LocationId, Guid BalanceId)
    {
        public static TestIds Create() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
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
