using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using XFramework.Domain.Contexts;
using XFramework.Domain.Shared.Contracts.Base;
using XFramework.Domain.Shared.Security;

namespace XFramework.Core.Tests.DataContext;

[TestFixture]
public sealed class XDbContextTrustedTenantTests
{
    [Test]
    public async Task QueryFilter_UsesTrustedTenantInsteadOfConflictingHttpClaim()
    {
        var trustedTenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var options = Options();
        var seedAuthorization = new TestCrossTenantWriteAuthorization();
        await using (var seed = new TestDbContext(
                         options,
                         new HttpContextAccessor(),
                         new ConfigurationBuilder().Build(),
                         new TestEffectiveTenantContextAccessor(trustedTenantId),
                         seedAuthorization))
        {
            seed.Entities.AddRange(Entity(trustedTenantId), Entity(otherTenantId));
            using (seedAuthorization.BeginTenantAdministrationScope())
                await seed.SaveChangesAsync();
        }

        var httpContext = new DefaultHttpContext();
        httpContext.User = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(
                [new System.Security.Claims.Claim("tenant_id", otherTenantId.ToString("D"))],
                "test"));
        await using var db = new TestDbContext(
            options,
            new HttpContextAccessor { HttpContext = httpContext },
            new ConfigurationBuilder().Build(),
            new TestEffectiveTenantContextAccessor(trustedTenantId));

        var rows = await db.Entities.ToListAsync();

        rows.Should().ContainSingle().Which.TenantId.Should().Be(trustedTenantId);
    }

    [Test]
    public async Task QueryFilter_RejectsClaimsAndConfigurationWhenTrustedInvocationIsMissing()
    {
        var fallbackTenantId = Guid.NewGuid();
        var options = Options();
        await using (var seed = new TestDbContext(
                         options,
                         new HttpContextAccessor(),
                         new ConfigurationBuilder().Build(),
                         new TestEffectiveTenantContextAccessor(fallbackTenantId)))
        {
            seed.Entities.Add(Entity(fallbackTenantId));
            await seed.SaveChangesAsync();
        }

        var httpContext = new DefaultHttpContext
        {
            User = new System.Security.Claims.ClaimsPrincipal(
                new System.Security.Claims.ClaimsIdentity(
                    [new System.Security.Claims.Claim("tenant_id", fallbackTenantId.ToString("D"))],
                    "test"))
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Tenant:DefaultId"] = fallbackTenantId.ToString("D")
            })
            .Build();
        await using var db = new TestDbContext(
            options,
            new HttpContextAccessor { HttpContext = httpContext },
            configuration,
            new TestEffectiveTenantContextAccessor(fallbackTenantId, hasTrustedInvocation: false));

        var query = async () => await db.Entities.ToListAsync();

        await query.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*trusted tenant context is required*");
    }

    [Test]
    public async Task QueryFilter_DoesNotUseClaimsOrConfigurationWithoutTrustedAccessor()
    {
        var untrustedTenantId = Guid.NewGuid();
        var httpContext = new DefaultHttpContext
        {
            User = new System.Security.Claims.ClaimsPrincipal(
                new System.Security.Claims.ClaimsIdentity(
                    [new System.Security.Claims.Claim("tenant_id", untrustedTenantId.ToString("D"))],
                    "test"))
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Tenant:DefaultId"] = untrustedTenantId.ToString("D")
            })
            .Build();
        await using var db = new TestDbContext(
            Options(),
            new HttpContextAccessor { HttpContext = httpContext },
            configuration);

        var query = async () => await db.Entities.ToListAsync();

        await query.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*trusted tenant context is required*");
    }

    [Test]
    public async Task SaveChanges_RejectsWriteWhenTrustedInvocationIsMissing()
    {
        var tenantId = Guid.NewGuid();
        var options = Options();
        await using var db = new TestDbContext(
            options,
            new HttpContextAccessor(),
            new ConfigurationBuilder().Build(),
            new TestEffectiveTenantContextAccessor(tenantId, hasTrustedInvocation: false));
        db.Entities.Add(Entity(tenantId));

        var save = async () => await db.SaveChangesAsync();

        await save.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*trusted tenant context is required*");
    }

    [Test]
    public async Task SaveChanges_OptionsOnlyConstruction_RejectsTrackedWrite()
    {
        var tenantId = Guid.NewGuid();
        await using var db = new TestDbContext(Options());
        db.Entities.Add(Entity(tenantId));

        var save = async () => await db.SaveChangesAsync();

        await save.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*trusted tenant context is required*");
    }

    [Test]
    public async Task SaveChanges_RejectsEntityOutsideTrustedTenant()
    {
        var options = Options();
        await using var db = new TestDbContext(
            options,
            new HttpContextAccessor(),
            new ConfigurationBuilder().Build(),
            new TestEffectiveTenantContextAccessor(Guid.NewGuid()));
        db.Entities.Add(Entity(Guid.NewGuid()));

        var save = async () => await db.SaveChangesAsync();

        await save.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*outside the trusted effective tenant*");
    }

    [Test]
    public async Task SaveChanges_AllowsEntityOutsideTrustedTenantOnlyDuringExplicitScope()
    {
        var options = Options();
        var authorization = new TestCrossTenantWriteAuthorization();
        await using var db = new TestDbContext(
            options,
            new HttpContextAccessor(),
            new ConfigurationBuilder().Build(),
            new TestEffectiveTenantContextAccessor(Guid.NewGuid()),
            authorization);
        db.Entities.Add(Entity(Guid.NewGuid()));

        int saved;
        using (authorization.BeginTenantAdministrationScope())
            saved = await db.SaveChangesAsync();

        saved.Should().Be(1);
        authorization.IsAuthorized.Should().BeFalse();
    }

    private static TestEntity Entity(Guid tenantId) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        IsEnabled = true,
        ConcurrencyStamp = Guid.NewGuid()
    };

    private static DbContextOptions<TestDbContext> Options() =>
        new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

    private sealed class TestDbContext : XDbContext
    {
        public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }

        public TestDbContext(
            DbContextOptions<TestDbContext> options,
            IHttpContextAccessor httpContextAccessor,
            IConfiguration configuration)
            : base(options, httpContextAccessor, configuration) { }

        public TestDbContext(
            DbContextOptions<TestDbContext> options,
            IHttpContextAccessor httpContextAccessor,
            IConfiguration configuration,
            IEffectiveTenantContextAccessor tenantContextAccessor)
            : base(options, httpContextAccessor, configuration, tenantContextAccessor) { }

        public TestDbContext(
            DbContextOptions<TestDbContext> options,
            IHttpContextAccessor httpContextAccessor,
            IConfiguration configuration,
            IEffectiveTenantContextAccessor tenantContextAccessor,
            ICrossTenantWriteAuthorizationAccessor crossTenantWriteAuthorizationAccessor)
            : base(
                options,
                httpContextAccessor,
                configuration,
                tenantContextAccessor,
                crossTenantWriteAuthorizationAccessor) { }

        public DbSet<TestEntity> Entities => Set<TestEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TestEntity>().HasKey(entity => entity.Id);
            base.OnModelCreating(modelBuilder);
        }
    }

    private sealed class TestEntity : BaseModel;

    private sealed class TestEffectiveTenantContextAccessor(
        Guid? tenantId,
        bool hasTrustedInvocation = true) : IEffectiveTenantContextAccessor
    {
        public bool HasTrustedInvocation => hasTrustedInvocation;
        public Guid? EffectiveTenantId => tenantId;
    }

    private sealed class TestCrossTenantWriteAuthorization :
        ICrossTenantWriteAuthorizationAccessor,
        ICrossTenantWriteAuthorizationScopeFactory
    {
        public bool IsAuthorized { get; private set; }

        public IDisposable BeginTenantAdministrationScope()
        {
            IsAuthorized = true;
            return new AuthorizationScope(this);
        }

        private sealed class AuthorizationScope(TestCrossTenantWriteAuthorization owner) : IDisposable
        {
            public void Dispose() => owner.IsAuthorized = false;
        }
    }
}
