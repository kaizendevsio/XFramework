using System.Collections;
using System.Linq.Expressions;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using XFramework.Core.Patterns;
using XFramework.Core.Services.Caching;
using XFramework.Domain.Contexts;
using XFramework.Domain.Shared.DataContext;
using XFramework.Inventario.Api.Services;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Enums;

namespace Inventario.Api.Tests;

[TestFixture]
public sealed class ProductServiceTests
{
    [Test]
    public async Task CreateAsync_AuthenticatedTenant_AssignsTenantAndCreatesOpeningMovement()
    {
        var tenantId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var dataContext = new FakeDataContext();
        dataContext.Set<ProductCategory>().Add(new ProductCategory
        {
            Id = categoryId,
            TenantId = tenantId,
            Name = "Parts"
        });

        var cache = new FakeCacheService();
        var service = CreateService(dataContext, cache, tenantId);

        var result = await service.CreateAsync(new CreateProductRequest
        {
            Name = "Widget",
            Description = "Tenant-owned product",
            Price = 12.50m,
            StockQuantity = 7,
            CategoryId = categoryId,
            SKU = "W-001"
        });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.StatusCode.Should().Be(201);
        result.Data!.TenantId.Should().Be(tenantId);
        result.Data.StockQuantity.Should().Be(7);

        var movement = dataContext.Added.OfType<InventoryMovement>().Single();
        movement.TenantId.Should().Be(tenantId);
        movement.ProductId.Should().Be(result.Data.Id);
        movement.MovementType.Should().Be(InventoryMovementType.OpeningBalance);
        movement.QuantityDelta.Should().Be(7);
        movement.QuantityBefore.Should().Be(0);
        movement.QuantityAfter.Should().Be(7);

        cache.SetKeys.Should().Contain($"products:{tenantId}:{result.Data.Id}");
    }

    [Test]
    public async Task CreateAsync_UnauthenticatedRequest_ReturnsUnauthorizedWithoutSaving()
    {
        var dataContext = new FakeDataContext();
        var service = CreateService(dataContext, new FakeCacheService(), tenantId: null);

        var result = await service.CreateAsync(new CreateProductRequest
        {
            Name = "Widget",
            Price = 12.50m,
            StockQuantity = 1,
            CategoryId = Guid.NewGuid()
        });

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(401);
        dataContext.Added.Should().BeEmpty();
        dataContext.SaveCount.Should().Be(0);
    }

    [Test]
    public async Task UpdateAsync_CatalogFieldsChange_DoesNotOverwriteStockQuantity()
    {
        var tenantId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var product = new Product
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Old",
            Price = 5m,
            StockQuantity = 12,
            CategoryId = categoryId,
            IsAvailable = true
        };

        var dataContext = new FakeDataContext();
        dataContext.Set<ProductCategory>().Add(new ProductCategory
        {
            Id = categoryId,
            TenantId = tenantId,
            Name = "Parts"
        });
        dataContext.Set<Product>().Add(product);

        var service = CreateService(dataContext, new FakeCacheService(), tenantId);

        var result = await service.UpdateAsync(product.Id, new UpdateProductRequest
        {
            Name = "New",
            Description = "Catalog edit",
            Price = 9m,
            CategoryId = categoryId,
            SKU = "W-002",
            Brand = "Acme",
            Weight = 1.5m,
            Image = "image.png",
            IsAvailable = false
        });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Data!.StockQuantity.Should().Be(12);
        product.StockQuantity.Should().Be(12);
        dataContext.Added.OfType<InventoryMovement>().Should().BeEmpty();
        dataContext.Updated.Should().ContainSingle(x => ReferenceEquals(x, product));
    }

    [Test]
    public void ModelConfiguration_StockBalanceAndReservation_DefinesConcurrencyAndBalanceUniqueness()
    {
        using var db = CreateModelOnlyDbContext(Guid.NewGuid());

        var stockBalance = db.Model.FindEntityType(typeof(StockBalance));
        stockBalance.Should().NotBeNull();
        stockBalance!.FindProperty(nameof(StockBalance.ConcurrencyStamp))!
            .IsConcurrencyToken.Should().BeTrue();

        var expectedBalanceIndex = new[]
        {
            nameof(StockBalance.TenantId),
            nameof(StockBalance.ProductId),
            nameof(StockBalance.WarehouseId),
            nameof(StockBalance.LocationId)
        };

        var hasUniqueBalanceIndex = stockBalance.GetIndexes()
            .Any(index => index.IsUnique
                && index.Properties.Select(p => p.Name).SequenceEqual(expectedBalanceIndex));
        hasUniqueBalanceIndex.Should().BeTrue();

        var reservation = db.Model.FindEntityType(typeof(Reservation));
        reservation.Should().NotBeNull();
        reservation!.FindProperty(nameof(Reservation.ConcurrencyStamp))!
            .IsConcurrencyToken.Should().BeTrue();
    }

    private static ProductService CreateService(
        FakeDataContext dataContext,
        FakeCacheService cache,
        Guid? tenantId)
    {
        var httpContextAccessor = new HttpContextAccessor();
        if (tenantId.HasValue)
        {
            httpContextAccessor.HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim("tenantId", tenantId.Value.ToString())],
                    authenticationType: "Test"))
            };
        }

        return new ProductService(
            dataContext,
            cache,
            NullLogger<ProductService>.Instance,
            httpContextAccessor);
    }

    private static AppDbContext CreateModelOnlyDbContext(Guid tenantId)
    {
        _ = typeof(StockBalance);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=xframework_inventario_model;Username=test;Password=test")
            .Options;

        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim("tenantId", tenantId.ToString())],
                    authenticationType: "Test"))
            }
        };

        var configuration = new ConfigurationBuilder().Build();
        return new AppDbContext(options, httpContextAccessor, configuration);
    }

    private sealed class FakeDataContext : IDataContext
    {
        private readonly Dictionary<Type, IList> sets = [];

        public List<object> Added { get; } = [];
        public List<object> Updated { get; } = [];
        public List<object> Removed { get; } = [];
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
            Removed.Add(entity);

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

        public IRemoteQuery<T> Where(Expression<Func<T, bool>> predicate)
        {
            query = query.Where(predicate);
            return this;
        }

        public IRemoteQuery<T> OrderBy<TKey>(Expression<Func<T, TKey>> keySelector)
        {
            query = query.OrderBy(keySelector);
            return this;
        }

        public IRemoteQuery<T> OrderByDescending<TKey>(Expression<Func<T, TKey>> keySelector)
        {
            query = query.OrderByDescending(keySelector);
            return this;
        }

        public IRemoteQuery<T> ThenBy<TKey>(Expression<Func<T, TKey>> keySelector)
        {
            query = ((IOrderedQueryable<T>)query).ThenBy(keySelector);
            return this;
        }

        public IRemoteQuery<T> ThenByDescending<TKey>(Expression<Func<T, TKey>> keySelector)
        {
            query = ((IOrderedQueryable<T>)query).ThenByDescending(keySelector);
            return this;
        }

        public IRemoteQuery<T> Skip(int count)
        {
            query = query.Skip(count);
            return this;
        }

        public IRemoteQuery<T> Take(int count)
        {
            query = query.Take(count);
            return this;
        }

        public IRemoteQuery<T> Include<TProperty>(Expression<Func<T, TProperty>> navigationSelector) => this;

        public IRemoteQuery<T> Distinct()
        {
            query = query.Distinct();
            return this;
        }

        public IRemoteQuery<T> DistinctBy<TKey>(Expression<Func<T, TKey>> keySelector)
        {
            query = query.AsEnumerable().DistinctBy(keySelector.Compile()).AsQueryable();
            return this;
        }

        public IRemoteQuery<T> NoCache() => this;

        public IRemoteQuery<T> IgnoreQueryFilters() => this;

        public Task<List<T>> ToListAsync(CancellationToken ct = default) =>
            Task.FromResult(query.ToList());

        public Task<T?> FirstOrDefaultAsync(CancellationToken ct = default) =>
            Task.FromResult(query.FirstOrDefault());

        public Task<T?> SingleOrDefaultAsync(CancellationToken ct = default) =>
            Task.FromResult(query.SingleOrDefault());

        public async IAsyncEnumerable<T> ToAsyncEnumerable(int chunkSize = 100, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            foreach (var item in query)
            {
                ct.ThrowIfCancellationRequested();
                yield return item;
                await Task.Yield();
            }
        }

        public Task<int> CountAsync(CancellationToken ct = default) =>
            Task.FromResult(query.Count());

        public Task<bool> AnyAsync(CancellationToken ct = default) =>
            Task.FromResult(query.Any());

        public Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default) =>
            Task.FromResult(query.Any(predicate));

        public Task<bool> AllAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default) =>
            Task.FromResult(query.All(predicate));

        public Task<TResult?> MinAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken ct = default) =>
            Task.FromResult(query.Select(selector).Min());

        public Task<TResult?> MaxAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken ct = default) =>
            Task.FromResult(query.Select(selector).Max());

        public Task<T?> MinByAsync<TKey>(Expression<Func<T, TKey>> keySelector, CancellationToken ct = default) =>
            Task.FromResult(query.AsEnumerable().MinBy(keySelector.Compile()));

        public Task<T?> MaxByAsync<TKey>(Expression<Func<T, TKey>> keySelector, CancellationToken ct = default) =>
            Task.FromResult(query.AsEnumerable().MaxBy(keySelector.Compile()));

        public Task<decimal> SumAsync(Expression<Func<T, decimal>> selector, CancellationToken ct = default) =>
            Task.FromResult(query.Sum(selector));

        public Task<double> AverageAsync(Expression<Func<T, decimal>> selector, CancellationToken ct = default) =>
            Task.FromResult((double)query.Average(selector));

        public Task<List<GroupResult<TKey, T>>> GroupByAsync<TKey>(
            Expression<Func<T, TKey>> keySelector,
            CancellationToken ct = default)
        {
            var compiled = keySelector.Compile();
            return Task.FromResult(query
                .AsEnumerable()
                .GroupBy(compiled)
                .Select(g => new GroupResult<TKey, T> { Key = g.Key, Items = g.ToList() })
                .ToList());
        }
    }

    private sealed class FakeCacheService : ICacheService
    {
        public List<string> SetKeys { get; } = [];

        public Task<Result<T?>> GetAsync<T>(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<T?>.Success(default));

        public Task<Result> SetAsync<T>(
            string key,
            T value,
            TimeSpan? absoluteExpiration = null,
            TimeSpan? slidingExpiration = null,
            CancellationToken cancellationToken = default)
        {
            SetKeys.Add(key);
            return Task.FromResult(Result.Success());
        }

        public Task<Result> RemoveAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success());

        public Task<Result<bool>> ExistsAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<bool>.Success(false));

        public Task<Result<T>> GetOrSetAsync<T>(
            string key,
            Func<CancellationToken, Task<T>> factory,
            TimeSpan? absoluteExpiration = null,
            TimeSpan? slidingExpiration = null,
            CancellationToken cancellationToken = default) =>
            factory(cancellationToken).ContinueWith(t => Result<T>.Success(t.Result), cancellationToken);

        public Task<Result<int>> RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<int>.Success(0));

        public Result<CacheStatistics> GetStatistics() =>
            Result<CacheStatistics>.Success(new CacheStatistics());

        public Task<Result> ClearAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success());
    }
}
