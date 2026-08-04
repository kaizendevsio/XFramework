using System.Linq.Expressions;
using FluentAssertions;
using IdentityServer.Api.Services;
using IdentityServer.Domain.Shared.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.DataContext;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Security;

namespace IdentityServer.UnitTests;

[TestFixture]
public sealed class ServiceIdentitySigningKeyConcurrencyTests
{
    private string _signingKeyDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        _signingKeyDirectory = Path.Combine(
            Path.GetTempPath(),
            "xframework-signing-key-concurrency",
            Guid.NewGuid().ToString("N"));
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_signingKeyDirectory))
            Directory.Delete(_signingKeyDirectory, recursive: true);
    }

    [Test]
    public async Task GetSigningKeys_ConcurrentEmptyStoreBootstrap_CreatesExactlyOneActiveKey()
    {
        const int callerCount = 12;
        var dataContext = new ConcurrentSigningKeyDataContext(callerCount);
        var configuration = CreateConfiguration();
        var serviceConfiguration = ServiceIdentityConfiguration.FromConfiguration(
            configuration,
            DateTimeOffset.UtcNow,
            "Test");

        var results = await Task.WhenAll(Enumerable.Range(0, callerCount).Select(_ =>
        {
            var service = new ServiceIdentityService(
                dataContext,
                serviceConfiguration,
                Mock.Of<IBoltTransportTokenSigner>(),
                TimeProvider.System,
                NullLogger<ServiceIdentityService>.Instance,
                signingKeyStore: new FileSystemServiceSigningKeyStore(configuration, serviceConfiguration));
            return service.GetSigningKeysAsync(new GetServiceSigningKeysRequest());
        }));

        results.Should().OnlyContain(result => result.IsSuccess);
        results
            .Select(result => result.Data!.Keys.Single(key => key.IsActive).KeyId)
            .Distinct()
            .Should().ContainSingle();
        dataContext.SigningKeys.Should().ContainSingle();
        dataContext.SigningKeys.Single().IsActive.Should().BeTrue();
        dataContext.SigningKeys.Single().CreatedBy.Should().Be("auto-bootstrap");
    }

    [Test]
    public async Task RotateSigningKey_ExplicitAdminRequest_StillCreatesANewActiveKey()
    {
        var dataContext = new ConcurrentSigningKeyDataContext(expectedBootstrapCallers: 1);
        var configuration = CreateConfiguration();
        var serviceConfiguration = ServiceIdentityConfiguration.FromConfiguration(
            configuration,
            DateTimeOffset.UtcNow,
            "Test");
        var service = new ServiceIdentityService(
            dataContext,
            serviceConfiguration,
            Mock.Of<IBoltTransportTokenSigner>(),
            TimeProvider.System,
            NullLogger<ServiceIdentityService>.Instance,
            SuperAdminContext(),
            signingKeyStore: new FileSystemServiceSigningKeyStore(configuration, serviceConfiguration));

        var initial = await service.GetSigningKeysAsync(new GetServiceSigningKeysRequest());
        var initialKeyId = initial.Data!.Keys.Single(key => key.IsActive).KeyId;
        var rotated = await service.RotateSigningKeyAsync(new RotateServiceSigningKeyRequest
        {
            Reason = "explicit-test-rotation"
        });

        rotated.IsSuccess.Should().BeTrue(rotated.Message);
        rotated.Data!.KeyId.Should().NotBe(initialKeyId);
        dataContext.SigningKeys.Should().HaveCount(2);
        dataContext.SigningKeys.Should().ContainSingle(key => key.IsActive && key.KeyId == rotated.Data.KeyId);
        dataContext.SigningKeys.Should().ContainSingle(key =>
            !key.IsActive && key.KeyId == initialKeyId && key.RetiredAtUtc.HasValue);
    }

    private static TestTrustedInvocationContextAccessor SuperAdminContext()
    {
        var tenantId = Guid.NewGuid();
        return new TestTrustedInvocationContextAccessor(new TrustedInvocationContext(
            new TrustedActorIdentity(
                Guid.NewGuid(),
                Guid.NewGuid(),
                tenantId,
                Guid.NewGuid(),
                new HashSet<string>(["SuperAdmin"], StringComparer.OrdinalIgnoreCase),
                new HashSet<string>(),
                "g1",
                DateTimeOffset.UtcNow.AddMinutes(5)),
            Service: null,
            EffectiveTenantId: tenantId,
            RequestedTargetTenantId: null,
            CorrelationId: Guid.NewGuid()));
    }

    private IConfiguration CreateConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ServiceIdentity:Issuer"] = XFrameworkServiceNames.IdentityServer,
                ["ServiceIdentity:ServiceTokenSigningKeyDirectory"] = _signingKeyDirectory,
                ["ServiceIdentity:Clients:0:ClientId"] = XFrameworkServiceNames.IdentityServer,
                ["ServiceIdentity:Clients:0:GenerationId"] = "test-g1",
                ["ServiceIdentity:Clients:0:ClientSecret"] =
                    "signing-key-concurrency-test-secret-material-111111111111111111111111",
                ["ServiceIdentity:Clients:0:AllowedAudiences:0"] = XFrameworkServiceNames.IdentityServer,
                ["ServiceIdentity:Clients:0:AllowedScopes:0"] = XFrameworkServiceScopes.BoltService
            })
            .Build();

    private sealed class ConcurrentSigningKeyDataContext(int expectedBootstrapCallers) : IDataContext
    {
        private readonly Lock _lock = new();
        private readonly TaskCompletionSource _initialDiscoveriesCompleted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly List<ServiceSigningKey> _signingKeys = [];
        private int _initialDiscoveryCount;

        public IReadOnlyList<ServiceSigningKey> SigningKeys
        {
            get
            {
                lock (_lock)
                    return _signingKeys.ToList();
            }
        }

        public IRemoteQuery<T> Query<T>() where T : class
        {
            if (typeof(T) != typeof(ServiceSigningKey))
                throw new NotSupportedException($"Unexpected query type {typeof(T).Name}.");

            lock (_lock)
            {
                return new ConcurrentSigningKeyQuery<T>(
                    _signingKeys.Cast<T>().ToList(),
                    WaitForInitialDiscoveryAsync);
            }
        }

        public void Add<T>(T entity) where T : class
        {
            lock (_lock)
                _signingKeys.Add((ServiceSigningKey)(object)entity);
        }

        public void Update<T>(T entity) where T : class
        {
        }

        public void Remove<T>(T entity) where T : class
        {
            lock (_lock)
                _signingKeys.Remove((ServiceSigningKey)(object)entity);
        }

        public Task<DataContextResult> SaveChangesAsync(CancellationToken ct = default) =>
            Task.FromResult(DataContextResult.Success());

        private Task WaitForInitialDiscoveryAsync()
        {
            if (Interlocked.Increment(ref _initialDiscoveryCount) == expectedBootstrapCallers)
                _initialDiscoveriesCompleted.TrySetResult();
            return _initialDiscoveriesCompleted.Task;
        }
    }

    private sealed class ConcurrentSigningKeyQuery<T>(
        IEnumerable<T> items,
        Func<Task> waitForInitialDiscoveryAsync) : IRemoteQuery<T>
        where T : class
    {
        private IEnumerable<T> _items = items;
        private bool _isDescendingListQuery;

        public IRemoteQuery<T> Where(Expression<Func<T, bool>> predicate)
        {
            _items = _items.Where(predicate.Compile());
            return this;
        }

        public IRemoteQuery<T> OrderBy<TKey>(Expression<Func<T, TKey>> keySelector)
        {
            _items = _items.OrderBy(keySelector.Compile());
            return this;
        }

        public IRemoteQuery<T> OrderByDescending<TKey>(Expression<Func<T, TKey>> keySelector)
        {
            _items = _items.OrderByDescending(keySelector.Compile());
            _isDescendingListQuery = true;
            return this;
        }

        public IRemoteQuery<T> ThenBy<TKey>(Expression<Func<T, TKey>> keySelector)
        {
            _items = ((IOrderedEnumerable<T>)_items).ThenBy(keySelector.Compile());
            return this;
        }

        public IRemoteQuery<T> ThenByDescending<TKey>(Expression<Func<T, TKey>> keySelector)
        {
            _items = ((IOrderedEnumerable<T>)_items).ThenByDescending(keySelector.Compile());
            return this;
        }

        public IRemoteQuery<T> Skip(int count)
        {
            _items = _items.Skip(count);
            return this;
        }

        public IRemoteQuery<T> Take(int count)
        {
            _items = _items.Take(count);
            return this;
        }

        public async Task<List<T>> ToListAsync(CancellationToken ct = default)
        {
            var result = _items.ToList();
            if (_isDescendingListQuery && result.Count == 0)
                await waitForInitialDiscoveryAsync().WaitAsync(ct);
            return result;
        }

        public Task<T?> FirstOrDefaultAsync(CancellationToken ct = default) =>
            Task.FromResult(_items.FirstOrDefault());

        public IRemoteQuery<T> Include<TProperty>(Expression<Func<T, TProperty>> navigationSelector) =>
            throw new NotSupportedException();
        public IRemoteQuery<T> Distinct() => throw new NotSupportedException();
        public IRemoteQuery<T> DistinctBy<TKey>(Expression<Func<T, TKey>> keySelector) =>
            throw new NotSupportedException();
        public IRemoteQuery<T> NoCache() => this;
        public IRemoteQuery<T> IgnoreQueryFilters() => this;
        public Task<T?> SingleOrDefaultAsync(CancellationToken ct = default) =>
            throw new NotSupportedException();
        public IAsyncEnumerable<T> ToAsyncEnumerable(int chunkSize = 100, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<int> CountAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> AnyAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<bool> AllAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<TResult?> MinAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<TResult?> MaxAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<T?> MinByAsync<TKey>(Expression<Func<T, TKey>> keySelector, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<T?> MaxByAsync<TKey>(Expression<Func<T, TKey>> keySelector, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<decimal> SumAsync(Expression<Func<T, decimal>> selector, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<double> AverageAsync(Expression<Func<T, decimal>> selector, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<List<GroupResult<TKey, T>>> GroupByAsync<TKey>(
            Expression<Func<T, TKey>> keySelector,
            CancellationToken ct = default) => throw new NotSupportedException();
    }
}
