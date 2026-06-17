using System;
using System.Threading.Tasks;
using FluentAssertions;
using IdentityServer.Domain.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using XFramework.Core.Services.FeatureGates;
using XFramework.Domain.Contexts;

namespace XFramework.Core.Tests.Services.FeatureGates;

[TestFixture]
public sealed class TenantModuleFeatureServiceTests
{
    [Test]
    public async Task IsEnabledAsync_EnabledFeatureForTenant_ReturnsTrue()
    {
        await using var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        SeedFeature(db, tenantId, TenantModuleFeatureKeys.Wallets, string.Empty, enabled: true);
        await db.SaveChangesAsync();

        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = CreateService(db, cache);

        var result = await service.IsEnabledAsync(tenantId, TenantModuleFeatureKeys.Wallets);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().BeTrue();
    }

    [Test]
    public async Task EnsureEnabledAsync_DisabledFeature_ReturnsForbidden()
    {
        await using var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        SeedFeature(db, tenantId, TenantModuleFeatureKeys.Inventario, string.Empty, enabled: false);
        await db.SaveChangesAsync();

        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = CreateService(db, cache);

        var result = await service.EnsureEnabledAsync(tenantId, TenantModuleFeatureKeys.Inventario);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
        result.Message.Should().Contain("Feature disabled");
    }

    [Test]
    public async Task IsEnabledAsync_FeatureEnabledForDifferentTenant_ReturnsFalse()
    {
        await using var db = CreateDbContext();
        var enabledTenantId = Guid.NewGuid();
        var disabledTenantId = Guid.NewGuid();
        SeedFeature(db, enabledTenantId, TenantModuleFeatureKeys.Messaging, TenantModuleFeatureKeys.ChatSubFeature, enabled: true);
        await db.SaveChangesAsync();

        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = CreateService(db, cache);

        var result = await service.IsEnabledAsync(disabledTenantId, TenantModuleFeatureKeys.MessagingChat);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().BeFalse();
    }

    [Test]
    public async Task Invalidate_FeatureUpdatedAfterCachedRead_RefreshesValue()
    {
        await using var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var feature = SeedFeature(db, tenantId, TenantModuleFeatureKeys.Notifications, string.Empty, enabled: false);
        await db.SaveChangesAsync();

        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = CreateService(db, cache);

        var cachedDisabled = await service.IsEnabledAsync(tenantId, TenantModuleFeatureKeys.Notifications);
        feature.IsEnabled = true;
        db.Update(feature);
        await db.SaveChangesAsync();

        var staleRead = await service.IsEnabledAsync(tenantId, TenantModuleFeatureKeys.Notifications);
        service.Invalidate(tenantId, TenantModuleFeatureKeys.Notifications);
        var refreshedRead = await service.IsEnabledAsync(tenantId, TenantModuleFeatureKeys.Notifications);

        cachedDisabled.Data.Should().BeFalse();
        staleRead.Data.Should().BeFalse();
        refreshedRead.Data.Should().BeTrue();
    }

    private static AppDbContext CreateDbContext()
    {
        _ = typeof(TenantModuleFeature);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new AppDbContext(options);
    }

    private static TenantModuleFeatureService CreateService(AppDbContext db, IMemoryCache cache) =>
        new(db, cache, NullLogger<TenantModuleFeatureService>.Instance);

    private static TenantModuleFeature SeedFeature(
        AppDbContext db,
        Guid tenantId,
        string moduleKey,
        string subFeatureKey,
        bool enabled)
    {
        var (normalizedModuleKey, normalizedSubFeatureKey) =
            TenantModuleFeatureKeys.Normalize(moduleKey, subFeatureKey);

        var feature = new TenantModuleFeature
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ModuleKey = normalizedModuleKey,
            SubFeatureKey = normalizedSubFeatureKey,
            DisplayName = TenantModuleFeatureKeys.Find(moduleKey, subFeatureKey)?.DisplayName,
            Description = TenantModuleFeatureKeys.Find(moduleKey, subFeatureKey)?.Description,
            CreatedAt = DateTime.UtcNow,
            IsEnabled = enabled
        };

        db.Set<TenantModuleFeature>().Add(feature);
        return feature;
    }
}
