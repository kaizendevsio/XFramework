using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using MemoryPack;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.DataContext;
using XFramework.Integration.DataContext;
using XFramework.Integration.DataContext.Cache;
using XFramework.Integration.Security;

namespace XFramework.Core.Tests.DataContext;

[TestFixture]
[NonParallelizable]
public sealed partial class CachingQuerySecurityTests
{
    private FieldInfo _wrapperMapField = null!;
    private object? _originalWrapperMap;

    [SetUp]
    public void SetUp()
    {
        _wrapperMapField = typeof(RemoteDataContext).GetField(
            "_wrapperMap",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        _wrapperMapField.Should().NotBeNull();
        _originalWrapperMap = _wrapperMapField.GetValue(null);
        _wrapperMapField.SetValue(null, new Dictionary<string, string>
        {
            [nameof(CachePartitionEntity)] = typeof(CachePartitionWrapper).FullName!
        });
    }

    [TearDown]
    public void TearDown() => _wrapperMapField.SetValue(null, _originalWrapperMap);

    [Test]
    public async Task CachedQueries_PartitionKeysByTenantAndCredential()
    {
        var tenantOne = Guid.NewGuid();
        var tenantTwo = Guid.NewGuid();
        var credentialOne = Guid.NewGuid();
        var credentialTwo = Guid.NewGuid();
        var cache = new RecordingClientCacheService();
        using var services = CreateServices();

        await ExecuteCachedQueryAsync(services, cache, tenantOne, credentialOne);
        await ExecuteCachedQueryAsync(services, cache, tenantOne, credentialTwo);
        await ExecuteCachedQueryAsync(services, cache, tenantTwo, credentialOne);

        cache.SetKeys.Should().OnlyHaveUniqueItems().And.HaveCount(3);
        cache.SetKeys.Should().Contain(key =>
            key.Contains($"tenant:{tenantOne:N}", StringComparison.OrdinalIgnoreCase));
        cache.SetKeys.Should().Contain(key =>
            key.Contains($"tenant:{tenantTwo:N}", StringComparison.OrdinalIgnoreCase));
        cache.SetKeys.Should().OnlyContain(key =>
            key.Contains(":authority:", StringComparison.OrdinalIgnoreCase));
    }

    [Test]
    public async Task CachedQueries_PartitionKeysBySessionGenerationAndCapabilities()
    {
        var tenantId = Guid.NewGuid();
        var credentialId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var cache = new RecordingClientCacheService();
        using var services = CreateServices();

        await ExecuteCachedQueryAsync(
            services,
            cache,
            CreateTrustedContext(tenantId, credentialId, sessionId, "generation-1", Capabilities("inventory.read")));
        await ExecuteCachedQueryAsync(
            services,
            cache,
            CreateTrustedContext(tenantId, credentialId, Guid.NewGuid(), "generation-1", Capabilities("inventory.read")));
        await ExecuteCachedQueryAsync(
            services,
            cache,
            CreateTrustedContext(tenantId, credentialId, sessionId, "generation-2", Capabilities("inventory.read")));
        await ExecuteCachedQueryAsync(
            services,
            cache,
            CreateTrustedContext(
                tenantId,
                credentialId,
                sessionId,
                "generation-1",
                Capabilities("inventory.read", "inventory.write")));

        cache.SetKeys.Should().OnlyHaveUniqueItems().And.HaveCount(4);
        cache.SetKeys.Should().OnlyContain(key =>
            key.Contains($"tenant:{tenantId:N}", StringComparison.OrdinalIgnoreCase));
    }

    [Test]
    public async Task CachedQueries_UseStableAuthorizationPartitionRegardlessOfCapabilityOrder()
    {
        var tenantId = Guid.NewGuid();
        var credentialId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var cache = new RecordingClientCacheService();
        using var services = CreateServices();

        await ExecuteCachedQueryAsync(
            services,
            cache,
            CreateTrustedContext(
                tenantId,
                credentialId,
                sessionId,
                "generation-1",
                Capabilities("inventory.read", "inventory.write")));
        await ExecuteCachedQueryAsync(
            services,
            cache,
            CreateTrustedContext(
                tenantId,
                credentialId,
                sessionId,
                "generation-1",
                Capabilities("inventory.write", "inventory.read")));

        cache.SetKeys.Should().HaveCount(2);
        cache.SetKeys.Should().OnlyContain(key => key == cache.SetKeys[0]);
    }

    [Test]
    public async Task CachedQueries_PartitionKeysByValidatedActorAttributes()
    {
        var tenantId = Guid.NewGuid();
        var credentialId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var cache = new RecordingClientCacheService();
        using var services = CreateServices();

        await ExecuteCachedQueryAsync(
            services,
            cache,
            CreateTrustedContext(
                tenantId,
                credentialId,
                sessionId,
                attributes: new Dictionary<string, string> { ["identity.is_verified"] = bool.TrueString }));
        await ExecuteCachedQueryAsync(
            services,
            cache,
            CreateTrustedContext(
                tenantId,
                credentialId,
                sessionId,
                attributes: new Dictionary<string, string> { ["identity.is_verified"] = bool.FalseString }));

        cache.SetKeys.Should().OnlyHaveUniqueItems().And.HaveCount(2);
    }

    [Test]
    public async Task CachedQuery_WithoutTrustedTenant_UsesTenantlessAuthorizationPartition()
    {
        var cache = new RecordingClientCacheService();
        var wrapper = new CachePartitionWrapper();
        using var services = new ServiceCollection()
            .AddSingleton(wrapper)
            .BuildServiceProvider();
        var remoteQuery = new RemoteQuery<CachePartitionEntity>(
            services,
            [],
            new RequestMetadata { RequestedTenantId = Guid.NewGuid() });
        var query = new CachingQuery<CachePartitionEntity>(
            remoteQuery,
            cache,
            new CachePolicy { Enabled = true },
            NullLogger.Instance,
            CreateTrustedContext());

        await query.ToListAsync();

        wrapper.QueryCount.Should().Be(1);
        cache.GetKeys.Should().ContainSingle()
            .Which.Should().Contain("tenant:tenantless");
        cache.SetKeys.Should().ContainSingle()
            .Which.Should().Contain("tenant:tenantless");
    }

    [Test]
    public async Task CachedQuery_WhenCacheFails_FallsBackToRemoteQuery()
    {
        var wrapper = new CachePartitionWrapper();
        using var services = new ServiceCollection().AddSingleton(wrapper).BuildServiceProvider();
        var remoteQuery = new RemoteQuery<CachePartitionEntity>(
            services,
            [],
            new RequestMetadata { RequestedTenantId = Guid.NewGuid() });
        var tenantId = Guid.NewGuid();
        var query = new CachingQuery<CachePartitionEntity>(
            remoteQuery,
            new ThrowingClientCacheService(),
            new CachePolicy { Enabled = true },
            NullLogger.Instance,
            CreateTrustedContext(tenantId, Guid.NewGuid()));

        var result = await query.ToListAsync();

        result.Should().BeEmpty();
        wrapper.QueryCount.Should().Be(1);
    }

    [Test]
    public async Task SaveChanges_WhenInvalidationFails_ReturnsCommittedResult()
    {
        var context = new CachingDataContext(
            new SuccessfulDataContext(),
            new ThrowingClientCacheService(),
            new DataContextOptions(),
            NullLogger<CachingDataContext>.Instance,
            CreateTrustedContext());

        context.Add(new CachePartitionEntity { Id = Guid.NewGuid() });
        var result = await context.SaveChangesAsync();

        result.IsSuccess.Should().BeTrue();
    }

    [Test]
    public async Task ItemInvalidation_RemovesAllPartitionedQueriesForEntityType()
    {
        var cache = new RecordingClientCacheService();
        var context = new CachingDataContext(
            new SuccessfulDataContext(),
            cache,
            new DataContextOptions(),
            NullLogger<CachingDataContext>.Instance,
            CreateTrustedContext());

        await context.InvalidateAsync<CachePartitionEntity>(Guid.NewGuid());

        cache.RemovedPrefixes.Should().ContainSingle()
            .Which.Should().Be("CachePartitionEntity:");
    }

    private static ServiceProvider CreateServices() => new ServiceCollection()
        .AddSingleton<CachePartitionWrapper>()
        .BuildServiceProvider();

    private static HashSet<string> Capabilities(params string[] values) =>
        new(values, StringComparer.OrdinalIgnoreCase);

    private static async Task ExecuteCachedQueryAsync(
        IServiceProvider services,
        IClientCacheService cache,
        Guid tenantId,
        Guid credentialId)
        => await ExecuteCachedQueryAsync(
            services,
            cache,
            CreateTrustedContext(tenantId, credentialId));

    private static async Task ExecuteCachedQueryAsync(
        IServiceProvider services,
        IClientCacheService cache,
        ITrustedInvocationContextAccessor trustedContext)
    {
        var remoteQuery = new RemoteQuery<CachePartitionEntity>(
            services,
            [],
            new RequestMetadata
            {
                RequestedTenantId = trustedContext.Current?.EffectiveTenantId
            });
        var query = new CachingQuery<CachePartitionEntity>(
            remoteQuery,
            cache,
            new CachePolicy { Enabled = true },
            NullLogger.Instance,
            trustedContext);
        await query.ToListAsync();
    }

    private static ITrustedInvocationContextAccessor CreateTrustedContext(
        Guid? effectiveTenantId = null,
        Guid? credentialId = null,
        Guid? sessionId = null,
        string generationId = "test-actor-generation",
        IReadOnlySet<string>? capabilities = null,
        IReadOnlyDictionary<string, string>? attributes = null)
    {
        var actor = credentialId is { } trustedCredentialId
            ? new TrustedActorIdentity(
                trustedCredentialId,
                Guid.NewGuid(),
                effectiveTenantId ?? Guid.NewGuid(),
                sessionId ?? Guid.NewGuid(),
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                capabilities ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                generationId,
                DateTimeOffset.UtcNow.AddMinutes(5),
                attributes)
            : null;

        return new TestTrustedInvocationContextAccessor(new TrustedInvocationContext(
            actor,
            null,
            effectiveTenantId,
            effectiveTenantId,
            Guid.NewGuid()));
    }

    private sealed class TestTrustedInvocationContextAccessor(TrustedInvocationContext current)
        : ITrustedInvocationContextAccessor
    {
        public TrustedInvocationContext? Current => current;
    }

    [MemoryPackable]
    public partial class CachePartitionEntity
    {
        public Guid Id { get; set; }
    }

    private sealed class CachePartitionWrapper : IDataContextServiceWrapper
    {
        public int QueryCount { get; private set; }

        public Task<byte[]> ExecuteQueryAsync(byte[] queryDescriptorBytes, CancellationToken ct = default)
        {
            QueryCount++;
            return Task.FromResult(MemoryPackSerializer.Serialize(new List<CachePartitionEntity>()));
        }

        public Task<byte[]> ExecuteChangesAsync(byte[] saveChangesRequestBytes, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public async IAsyncEnumerable<byte[]> ExecuteQueryStreamAsync(
            byte[] queryDescriptorBytes,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            await Task.CompletedTask;
            yield break;
        }
    }

    private sealed class RecordingClientCacheService : IClientCacheService
    {
        public List<string> GetKeys { get; } = [];
        public List<string> SetKeys { get; } = [];
        public List<string> RemovedPrefixes { get; } = [];

        public Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
        {
            GetKeys.Add(key);
            return Task.FromResult<T?>(default);
        }

        public Task SetAsync<T>(string key, T value, TimeSpan expiration, CancellationToken ct = default)
        {
            SetKeys.Add(key);
            return Task.CompletedTask;
        }

        public Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default)
        {
            RemovedPrefixes.Add(prefix);
            return Task.CompletedTask;
        }

        public Task ClearAllAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class ThrowingClientCacheService : IClientCacheService
    {
        public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) =>
            throw new InvalidOperationException("cache unavailable");

        public Task SetAsync<T>(string key, T value, TimeSpan expiration, CancellationToken ct = default) =>
            throw new InvalidOperationException("cache unavailable");

        public Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default) =>
            throw new InvalidOperationException("cache unavailable");

        public Task ClearAllAsync(CancellationToken ct = default) =>
            throw new InvalidOperationException("cache unavailable");
    }

    private sealed class SuccessfulDataContext : IDataContext
    {
        public IRemoteQuery<T> Query<T>() where T : class => throw new NotSupportedException();
        public void Add<T>(T entity) where T : class { }
        public void Update<T>(T entity) where T : class { }
        public void Remove<T>(T entity) where T : class { }
        public Task<DataContextResult> SaveChangesAsync(CancellationToken ct = default) =>
            Task.FromResult(DataContextResult.Success());
    }
}
