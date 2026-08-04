using FluentAssertions;
using IdentityServer.Domain.Shared.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using XFramework.Domain.Contexts;
using XFramework.Domain.Shared.Security;

namespace IdentityServer.UnitTests;

[TestFixture]
public sealed class TenantCrossTenantWriteSecurityTests
{
    [Test]
    public async Task OrdinaryActor_CannotWriteTenantOwnedByAnotherTenant()
    {
        var actorTenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<TestIdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        await using var db = new TestIdentityDbContext(
            options,
            new TestEffectiveTenantContextAccessor(actorTenantId));
        var targetTenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = targetTenantId,
            TenantId = targetTenantId,
            Name = "Other tenant",
            Version = 1,
            IsEnabled = true,
            ConcurrencyStamp = Guid.NewGuid()
        });

        var save = async () => await db.SaveChangesAsync();

        await save.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*outside the trusted effective tenant*");
    }

    private sealed class TestIdentityDbContext(
        DbContextOptions<TestIdentityDbContext> options,
        IEffectiveTenantContextAccessor tenantContextAccessor)
        : XDbContext(
            options,
            new HttpContextAccessor(),
            new ConfigurationBuilder().Build(),
            tenantContextAccessor)
    {
        public DbSet<Tenant> Tenants => Set<Tenant>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Tenant>(entity =>
            {
                entity.HasKey(tenant => tenant.Id);
                entity.Ignore(tenant => tenant.IdentityCredentials);
                entity.Ignore(tenant => tenant.IdentityInformations);
                entity.Ignore(tenant => tenant.IdentityRoleTypes);
                entity.Ignore(tenant => tenant.RegistryConfigurations);
                entity.Ignore(tenant => tenant.TenantModuleFeatures);
                entity.Ignore(tenant => tenant.AuthorizationPolicy);
            });
            base.OnModelCreating(modelBuilder);
        }
    }

    private sealed class TestEffectiveTenantContextAccessor(Guid tenantId) : IEffectiveTenantContextAccessor
    {
        public bool HasTrustedInvocation => true;
        public Guid? EffectiveTenantId => tenantId;
    }
}
