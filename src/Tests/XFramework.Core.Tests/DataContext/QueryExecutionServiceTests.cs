using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using IdentityServer.Domain.Shared.Contracts;
using MemoryPack;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using XFramework.Core.DataContext;
using XFramework.Core.Services.FeatureGates;
using XFramework.Core.Patterns;
using XFramework.Domain.Contexts;
using XFramework.Domain.Shared.Contracts.Base;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.DataContext;
using XFramework.Domain.Shared.Enums;
using XFramework.Domain.Shared.Security;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Security;

namespace XFramework.Core.Tests.DataContext;

[TestFixture]
public partial class QueryExecutionServiceTests
{
    private static readonly Guid DefaultTenantId = Guid.Parse("7602c2d3-01df-4bdb-9a67-02c144e4a2ac");

    [Test]
    public async Task ExecuteAsync_ResolvesDbContextFromAuthorizedInvocationScope()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var tenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<TestTenantEntityDbContext>()
            .UseSqlite(connection)
            .Options;
        var dbContextCreations = 0;
        var services = new ServiceCollection()
            .AddScoped<DbContext>(_ =>
            {
                Interlocked.Increment(ref dbContextCreations);
                return new TestTenantEntityDbContext(options);
            })
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

        await using var invocationScope = services.CreateAsyncScope();
        var dbContext = invocationScope.ServiceProvider.GetRequiredService<DbContext>();
        await dbContext.Database.EnsureCreatedAsync();
        dbContext.Add(new TestTenantEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Invocation scope"
        });
        await dbContext.SaveChangesAsync();

        var service = new QueryExecutionService(
            invocationScope.ServiceProvider,
            NullLogger<QueryExecutionService>.Instance,
            CreateTrustedContext(tenantId));
        service.RegisterEntity<TestTenantEntity>(nameof(TestTenantEntity));
        var descriptor = new QueryDescriptor
        {
            EntityTypeName = nameof(TestTenantEntity),
            Mode = QueryExecutionMode.ToList
        };

        var rows = MemoryPackSerializer.Deserialize<List<TestTenantEntity>>(
            await service.ExecuteAsync(MemoryPackSerializer.Serialize(descriptor)));

        rows.Should().ContainSingle().Which.Name.Should().Be("Invocation scope");
        dbContextCreations.Should().Be(1,
            "remote execution must reuse the already-authorized invocation scope");
    }

    [Test]
    public async Task GroupByWithoutTake_AppliesDefaultMaterializationLimit()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<TestTenantEntityDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new TestTenantEntityDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var tenantId = Guid.NewGuid();
        db.TestTenantEntities.AddRange(Enumerable.Range(0, QueryDescriptorExecutor.DefaultMaterializationLimit + 25)
            .Select(index => new TestTenantEntity
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = $"Group-{index:D4}"
            }));
        await db.SaveChangesAsync();

        var result = await QueryDescriptorExecutor.ExecuteAsync(
            db,
            typeof(TestTenantEntity),
            new QueryDescriptor
            {
                EntityTypeName = nameof(TestTenantEntity),
                Mode = QueryExecutionMode.GroupBy,
                GroupByProperty = nameof(TestTenantEntity.Name),
                Metadata = new RequestMetadata { RequestedTenantId = tenantId }
            },
            tenantId);

        result.Should().BeAssignableTo<System.Collections.ICollection>();
        ((System.Collections.ICollection)result!).Count
            .Should().Be(QueryDescriptorExecutor.DefaultMaterializationLimit);
    }

    [Test]
    public async Task ExecuteAsync_IdentityEntityWithDeniedActorCapability_ReturnsFailure()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var actorTenantId = Guid.NewGuid();
        var targetTenantId = Guid.NewGuid();
        var credentialId = Guid.NewGuid();

        var services = new ServiceCollection()
            .AddDbContext<QueryExecutionDbContext>(options => options.UseSqlite(connection))
            .AddScoped<DbContext>(provider => provider.GetRequiredService<QueryExecutionDbContext>())
            .BuildServiceProvider();

        await using (var scope = services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<DbContext>();
            await dbContext.Database.EnsureCreatedAsync();
        }

        var service = new QueryExecutionService(
            services,
            NullLogger<QueryExecutionService>.Instance,
            CreateTrustedContext(targetTenantId, CreateActor(actorTenantId, credentialId)));
        service.RegisterEntity<Tenant>(nameof(Tenant));
        var descriptor = new QueryDescriptor
        {
            EntityTypeName = nameof(Tenant),
            Mode = QueryExecutionMode.ToList,
            Metadata = new RequestMetadata
            {
                RequestedTenantId = targetTenantId
            }
        };

        var bytes = await service.ExecuteAsync(MemoryPackSerializer.Serialize(descriptor));
        var result = MemoryPackSerializer.Deserialize<DataContextResult>(bytes);

        result.Should().NotBeNull();
        result!.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Credential capability is not allowed for this operation.");
    }

    [Test]
    public async Task ExecuteChangesAsync_IdentityEntityWithDeniedActorCapability_DoesNotPersist()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var actorTenantId = Guid.NewGuid();
        var targetTenantId = Guid.NewGuid();
        var credentialId = Guid.NewGuid();

        var services = new ServiceCollection()
            .AddDbContext<QueryExecutionDbContext>(options => options.UseSqlite(connection))
            .AddScoped<DbContext>(provider => provider.GetRequiredService<QueryExecutionDbContext>())
            .BuildServiceProvider();

        await using (var scope = services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<DbContext>();
            await dbContext.Database.EnsureCreatedAsync();
        }

        var service = new QueryExecutionService(
            services,
            NullLogger<QueryExecutionService>.Instance,
            CreateTrustedContext(targetTenantId, CreateActor(actorTenantId, credentialId)));
        service.RegisterEntity(typeof(Tenant), nameof(Tenant), allowRemoteMutation: true);
        var request = new SaveChangesRequest
        {
            Metadata = new RequestMetadata
            {
                RequestedTenantId = targetTenantId
            },
            Changes =
            [
                new ChangeEntry
                {
                    EntityTypeName = nameof(Tenant),
                    Operation = ChangeOperation.Add,
                    SerializedEntity = MemoryPackSerializer.Serialize(new Tenant
                    {
                        Id = targetTenantId,
                        TenantId = targetTenantId,
                        Name = "Denied tenant",
                        IsEnabled = true,
                        CreatedAt = DateTime.UtcNow
                    })
                }
            ]
        };

        var bytes = await service.ExecuteChangesAsync(MemoryPackSerializer.Serialize(request));
        var result = MemoryPackSerializer.Deserialize<DataContextResult>(bytes);

        result.Should().NotBeNull();
        result!.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Credential capability is not allowed for this operation.");

        await using var verifyScope = services.CreateAsyncScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<QueryExecutionDbContext>();
        (await db.Set<Tenant>().CountAsync()).Should().Be(0);
    }

    [Test]
    public async Task QueryDescriptorExecutor_ControlPlaneEntities_ReturnOnlyTrustedTenantRows()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<QueryExecutionDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new QueryExecutionDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var trustedTenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        db.Set<Tenant>().AddRange(
            CreateTenant(trustedTenantId, "Trusted"),
            CreateTenant(otherTenantId, "Other"));
        db.Set<TenantModuleFeature>().AddRange(
            CreateTenantModuleFeature(trustedTenantId, "Trusted feature"),
            CreateTenantModuleFeature(otherTenantId, "Other feature"));
        await db.SaveChangesAsync();

        var tenantRows = (List<Tenant>)(await QueryDescriptorExecutor.ExecuteAsync(
            db,
            typeof(Tenant),
            new QueryDescriptor { EntityTypeName = nameof(Tenant), Mode = QueryExecutionMode.ToList },
            trustedTenantId))!;
        var featureRows = (List<TenantModuleFeature>)(await QueryDescriptorExecutor.ExecuteAsync(
            db,
            typeof(TenantModuleFeature),
            new QueryDescriptor { EntityTypeName = nameof(TenantModuleFeature), Mode = QueryExecutionMode.ToList },
            trustedTenantId))!;

        tenantRows.Select(row => row.Id).Should().Equal(trustedTenantId);
        featureRows.Select(row => row.TenantId).Should().OnlyContain(id => id == trustedTenantId);
    }

    [Test]
    public async Task ExecuteAsync_AuthorizedAllTenantQuery_ReturnsRowsAcrossTenants()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<QueryExecutionDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new QueryExecutionDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var actorTenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        db.Set<Tenant>().AddRange(
            CreateTenant(actorTenantId, "Actor tenant"),
            CreateTenant(otherTenantId, "Other tenant"));
        await db.SaveChangesAsync();

        var actor = new TrustedActorIdentity(
            Guid.NewGuid(),
            Guid.NewGuid(),
            actorTenantId,
            Guid.NewGuid(),
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(
                ["identity.tenants:view", "identity.tenants:manage"],
                StringComparer.OrdinalIgnoreCase),
            "test-actor-generation",
            DateTimeOffset.UtcNow.AddMinutes(5));
        var services = new ServiceCollection()
            .AddSingleton<DbContext>(db)
            .AddSingleton(CreateEnabledTenantFeatureService(actorTenantId).Object)
            .BuildServiceProvider();
        var service = new QueryExecutionService(
            services,
            NullLogger<QueryExecutionService>.Instance,
            CreateTrustedContext(actorTenantId, actor));
        service.RegisterEntity<Tenant>(nameof(Tenant));
        var descriptor = new QueryDescriptor
        {
            EntityTypeName = nameof(Tenant),
            Mode = QueryExecutionMode.ToList,
            IgnoreQueryFilters = true
        };

        var bytes = await service.ExecuteAsync(MemoryPackSerializer.Serialize(descriptor));
        var tenants = MemoryPackSerializer.Deserialize<List<Tenant>>(bytes)!;

        tenants.Select(tenant => tenant.Id).Should().BeEquivalentTo([actorTenantId, otherTenantId]);
    }

    [Test]
    public async Task ExecuteAsync_ActorlessCallerWithGenericQueryScope_IsRejected()
    {
        var tenantId = Guid.NewGuid();
        var service = new QueryExecutionService(
            new ServiceCollection().BuildServiceProvider(),
            NullLogger<QueryExecutionService>.Instance,
            CreateTrustedContext(
                tenantId,
                serviceScopes: new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    XFrameworkServiceScopes.DataContextQuery
                }));
        service.RegisterEntity<TestTenantEntity>(nameof(TestTenantEntity));
        var descriptor = new QueryDescriptor
        {
            EntityTypeName = nameof(TestTenantEntity),
            Mode = QueryExecutionMode.ToList,
            Metadata = new RequestMetadata { RequestedTenantId = tenantId }
        };

        var result = MemoryPackSerializer.Deserialize<DataContextResult>(
            await service.ExecuteAsync(MemoryPackSerializer.Serialize(descriptor)))!;

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain(XFrameworkServiceScopes.TenantTarget);
    }

    [Test]
    public async Task ExecuteChangesAsync_ActorlessCallerWithGenericMutationScope_IsRejected()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var services = new ServiceCollection()
            .AddDbContext<TestTenantEntityDbContext>(options => options.UseSqlite(connection))
            .AddScoped<DbContext>(provider => provider.GetRequiredService<TestTenantEntityDbContext>())
            .BuildServiceProvider();
        var tenantId = Guid.NewGuid();
        var service = new QueryExecutionService(
            services,
            NullLogger<QueryExecutionService>.Instance,
            CreateTrustedContext(
                tenantId,
                serviceScopes: new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    XFrameworkServiceScopes.DataContextMutate
                }));
        service.RegisterEntity(typeof(TestTenantEntity), nameof(TestTenantEntity), allowRemoteMutation: true);
        var request = new SaveChangesRequest
        {
            Metadata = new RequestMetadata { RequestedTenantId = tenantId },
            Changes = [CreateAddChange(Guid.NewGuid(), tenantId, "Rejected")]
        };

        var result = MemoryPackSerializer.Deserialize<DataContextResult>(
            await service.ExecuteChangesAsync(MemoryPackSerializer.Serialize(request)))!;

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain(XFrameworkServiceScopes.TenantTarget);
    }

    [Test]
    public async Task ExecuteChangesAsync_CrossTenantTenantUpdate_IsRejectedBeforePersistence()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var trustedTenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var featureService = CreateEnabledTenantFeatureService(trustedTenantId);
        var services = new ServiceCollection()
            .AddDbContext<QueryExecutionDbContext>(options => options.UseSqlite(connection))
            .AddScoped<DbContext>(provider => provider.GetRequiredService<QueryExecutionDbContext>())
            .AddSingleton(featureService.Object)
            .BuildServiceProvider();
        var otherTenant = CreateTenant(otherTenantId, "Other");
        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QueryExecutionDbContext>();
            await db.Database.EnsureCreatedAsync();
            db.Add(otherTenant);
            await db.SaveChangesAsync();
        }

        var patch = new FieldPatch
        {
            EntityId = MemoryPackSerializer.Serialize(otherTenantId),
            ExpectedConcurrencyStamp = otherTenant.ConcurrencyStamp,
            Changes = new Dictionary<string, byte[]>
            {
                [nameof(Tenant.Name)] = MemoryPackSerializer.Serialize<string?>("Changed")
            }
        };
        var request = new SaveChangesRequest
        {
            Metadata = new RequestMetadata { RequestedTenantId = trustedTenantId },
            Changes =
            [
                new ChangeEntry
                {
                    EntityTypeName = nameof(Tenant),
                    Operation = ChangeOperation.Update,
                    SerializedEntity = MemoryPackSerializer.Serialize(patch)
                }
            ]
        };
        var service = new QueryExecutionService(
            services,
            NullLogger<QueryExecutionService>.Instance,
            CreateTrustedContext(trustedTenantId));
        service.RegisterEntity(typeof(Tenant), nameof(Tenant), allowRemoteMutation: true);

        var result = MemoryPackSerializer.Deserialize<DataContextResult>(
            await service.ExecuteChangesAsync(MemoryPackSerializer.Serialize(request)))!;

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("does not match the trusted invocation tenant");
        await using var verifyScope = services.CreateAsyncScope();
        (await verifyScope.ServiceProvider.GetRequiredService<QueryExecutionDbContext>()
                .Set<Tenant>().SingleAsync(row => row.Id == otherTenantId))
            .Name.Should().Be("Other");
    }

    [Test]
    public async Task ExecuteChangesAsync_CrossTenantTenantModuleFeatureDelete_IsRejectedBeforePersistence()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var trustedTenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var featureService = CreateEnabledTenantFeatureService(trustedTenantId);
        var services = new ServiceCollection()
            .AddDbContext<QueryExecutionDbContext>(options => options.UseSqlite(connection))
            .AddScoped<DbContext>(provider => provider.GetRequiredService<QueryExecutionDbContext>())
            .AddSingleton(featureService.Object)
            .BuildServiceProvider();
        var otherTenant = CreateTenant(otherTenantId, "Other");
        var otherFeature = CreateTenantModuleFeature(otherTenantId, "Other feature");
        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QueryExecutionDbContext>();
            await db.Database.EnsureCreatedAsync();
            db.Add(otherTenant);
            db.Add(otherFeature);
            await db.SaveChangesAsync();
        }
        var request = new SaveChangesRequest
        {
            Metadata = new RequestMetadata { RequestedTenantId = trustedTenantId },
            Changes =
            [
                new ChangeEntry
                {
                    EntityTypeName = nameof(TenantModuleFeature),
                    Operation = ChangeOperation.Remove,
                    SerializedEntity = MemoryPackSerializer.Serialize(otherFeature)
                }
            ]
        };
        var service = new QueryExecutionService(
            services,
            NullLogger<QueryExecutionService>.Instance,
            CreateTrustedContext(trustedTenantId));
        service.RegisterEntity(typeof(TenantModuleFeature), nameof(TenantModuleFeature), allowRemoteMutation: true);

        var result = MemoryPackSerializer.Deserialize<DataContextResult>(
            await service.ExecuteChangesAsync(MemoryPackSerializer.Serialize(request)))!;

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("does not match the trusted invocation tenant");
        await using var verifyScope = services.CreateAsyncScope();
        (await verifyScope.ServiceProvider.GetRequiredService<QueryExecutionDbContext>()
            .Set<TenantModuleFeature>().AnyAsync(row => row.Id == otherFeature.Id)).Should().BeTrue();
    }

    [Test]
    public async Task ExecuteChangesAsync_DeleteWithForgedTenantId_ValidatesPersistedOwnership()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var trustedTenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var featureService = CreateEnabledTenantFeatureService(trustedTenantId);
        var services = new ServiceCollection()
            .AddDbContext<QueryExecutionDbContext>(options => options.UseSqlite(connection))
            .AddScoped<DbContext>(provider => provider.GetRequiredService<QueryExecutionDbContext>())
            .AddSingleton(featureService.Object)
            .BuildServiceProvider();
        var otherTenant = CreateTenant(otherTenantId, "Other");
        var otherFeature = CreateTenantModuleFeature(otherTenantId, "Other feature");
        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QueryExecutionDbContext>();
            await db.Database.EnsureCreatedAsync();
            db.Add(otherTenant);
            db.Add(otherFeature);
            await db.SaveChangesAsync();
        }

        var forgedFeature = CreateTenantModuleFeature(trustedTenantId, "Forged tenant");
        forgedFeature.Id = otherFeature.Id;
        forgedFeature.ConcurrencyStamp = otherFeature.ConcurrencyStamp;
        var request = new SaveChangesRequest
        {
            Metadata = new RequestMetadata { RequestedTenantId = trustedTenantId },
            Changes =
            [
                new ChangeEntry
                {
                    EntityTypeName = nameof(TenantModuleFeature),
                    Operation = ChangeOperation.Remove,
                    SerializedEntity = MemoryPackSerializer.Serialize(forgedFeature)
                }
            ]
        };
        var service = new QueryExecutionService(
            services,
            NullLogger<QueryExecutionService>.Instance,
            CreateTrustedContext(trustedTenantId));
        service.RegisterEntity(typeof(TenantModuleFeature), nameof(TenantModuleFeature), allowRemoteMutation: true);

        var result = MemoryPackSerializer.Deserialize<DataContextResult>(
            await service.ExecuteChangesAsync(MemoryPackSerializer.Serialize(request)))!;

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("does not match the trusted invocation tenant");
        await using var verifyScope = services.CreateAsyncScope();
        (await verifyScope.ServiceProvider.GetRequiredService<QueryExecutionDbContext>()
            .Set<TenantModuleFeature>().AnyAsync(row => row.Id == otherFeature.Id)).Should().BeTrue();
    }

    [Test]
    public async Task ExecuteAsync_FirstOrDefaultWithoutMatch_ReturnsSerializedNull()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var tenantId = Guid.NewGuid();
        var featureService = CreateEnabledTenantFeatureService(tenantId);

        var services = new ServiceCollection()
            .AddDbContext<QueryExecutionDbContext>(options => options.UseSqlite(connection))
            .AddScoped<DbContext>(provider => provider.GetRequiredService<QueryExecutionDbContext>())
            .AddSingleton(featureService.Object)
            .BuildServiceProvider();

        await using (var scope = services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<DbContext>();
            await dbContext.Database.EnsureCreatedAsync();
        }

        var service = new QueryExecutionService(
            services,
            NullLogger<QueryExecutionService>.Instance,
            CreateTrustedContext(tenantId));
        service.RegisterEntity<Tenant>("Tenant");

        var descriptor = new QueryDescriptor
        {
            EntityTypeName = "Tenant",
            Mode = QueryExecutionMode.FirstOrDefault
        };

        var resultBytes = await service.ExecuteAsync(MemoryPackSerializer.Serialize(descriptor));

        resultBytes.Should().NotBeEmpty();
        MemoryPackSerializer.Deserialize<Tenant>(resultBytes).Should().BeNull();
    }

    [Test]
    public async Task ExecuteAsync_MetadataFlagCannotBypassQueryFilters()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var hiddenTenantId = Guid.NewGuid();
        var featureService = CreateEnabledTenantFeatureService(DefaultTenantId);
        var seedAuthorization = new TestCrossTenantWriteAuthorizationAccessor();
        var services = new ServiceCollection()
            .AddSingleton<IHttpContextAccessor, HttpContextAccessor>()
            .AddSingleton<IEffectiveTenantContextAccessor>(
                new TestEffectiveTenantContextAccessor(DefaultTenantId))
            .AddSingleton<ICrossTenantWriteAuthorizationAccessor>(seedAuthorization)
            .AddSingleton<IConfiguration>(new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Tenant:DefaultId"] = DefaultTenantId.ToString()
                })
                .Build())
            .AddDbContext<FilteredQueryExecutionDbContext>(options => options.UseSqlite(connection))
            .AddScoped<DbContext>(provider => provider.GetRequiredService<FilteredQueryExecutionDbContext>())
            .AddSingleton(featureService.Object)
            .BuildServiceProvider();

        await using (var scope = services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<DbContext>();
            await dbContext.Database.EnsureCreatedAsync();
            dbContext.Add(new Tenant
            {
                Id = DefaultTenantId,
                TenantId = DefaultTenantId,
                Name = "Default Tenant",
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow
            });
            dbContext.Add(new Tenant
            {
                Id = hiddenTenantId,
                TenantId = hiddenTenantId,
                Name = "Hidden Tenant",
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }

        var service = new QueryExecutionService(
            services,
            NullLogger<QueryExecutionService>.Instance,
            CreateTrustedContext(DefaultTenantId));
        service.RegisterEntity<Tenant>("Tenant");

        var descriptor = new QueryDescriptor
        {
            EntityTypeName = "Tenant",
            Mode = QueryExecutionMode.ToList,
            Metadata = new RequestMetadata
            {
                UserAgent = "Portal;xframework.ignoreQueryFilters"
            }
        };

        var resultBytes = await service.ExecuteAsync(MemoryPackSerializer.Serialize(descriptor));
        var tenants = MemoryPackSerializer.Deserialize<List<Tenant>>(resultBytes);

        tenants.Should().NotBeNull();
        tenants!.Select(x => x.Id).Should().Equal(DefaultTenantId);
    }

    [Test]
    public async Task ExecuteAsync_TenantOwnedEntityWithoutTrustedTenant_ReturnsDataContextFailure()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var services = new ServiceCollection()
            .AddDbContext<TestTenantEntityDbContext>(options => options.UseSqlite(connection))
            .AddScoped<DbContext>(provider => provider.GetRequiredService<TestTenantEntityDbContext>())
            .BuildServiceProvider();

        await using (var setupScope = services.CreateAsyncScope())
        {
            var setupDbContext = setupScope.ServiceProvider.GetRequiredService<DbContext>();
            await setupDbContext.Database.EnsureCreatedAsync();
        }

        var service = new QueryExecutionService(
            services,
            NullLogger<QueryExecutionService>.Instance,
            CreateTrustedContext());
        service.RegisterEntity<TestTenantEntity>("TestTenantEntity");

        var descriptor = new QueryDescriptor
        {
            EntityTypeName = "TestTenantEntity",
            Mode = QueryExecutionMode.ToList
        };

        var resultBytes = await service.ExecuteAsync(MemoryPackSerializer.Serialize(descriptor));
        var result = MemoryPackSerializer.Deserialize<DataContextResult>(resultBytes);

        result.Should().NotBeNull();
        result!.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("trusted tenant");
    }

    [Test]
    public async Task ExecuteAsync_TenantOwnedEntityWithTrustedTenant_ReturnsSerializedRows()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var services = new ServiceCollection()
            .AddDbContext<TestTenantEntityDbContext>(options => options.UseSqlite(connection))
            .AddScoped<DbContext>(provider => provider.GetRequiredService<TestTenantEntityDbContext>())
            .BuildServiceProvider();

        var tenantId = Guid.NewGuid();
        await using (var setupScope = services.CreateAsyncScope())
        {
            var setupDbContext = setupScope.ServiceProvider.GetRequiredService<TestTenantEntityDbContext>();
            await setupDbContext.Database.EnsureCreatedAsync();
            setupDbContext.TestTenantEntities.Add(new TestTenantEntity
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = "Tenant scoped"
            });
            await setupDbContext.SaveChangesAsync();
        }

        var service = new QueryExecutionService(
            services,
            NullLogger<QueryExecutionService>.Instance,
            CreateTrustedContext(tenantId));
        service.RegisterEntity<TestTenantEntity>("TestTenantEntity");

        var descriptor = new QueryDescriptor
        {
            EntityTypeName = "TestTenantEntity",
            Mode = QueryExecutionMode.ToList,
            Metadata = new RequestMetadata { RequestedTenantId = tenantId }
        };

        var resultBytes = await service.ExecuteAsync(MemoryPackSerializer.Serialize(descriptor));
        var rows = MemoryPackSerializer.Deserialize<List<TestTenantEntity>>(resultBytes);

        rows.Should().NotBeNull();
        rows!.Should().ContainSingle(x => x.Name == "Tenant scoped");
    }

    [Test]
    public async Task ExecuteAsync_TenantOwnedEntityWithIgnoreFilters_StillAppliesTrustedTenantBoundary()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var services = new ServiceCollection()
            .AddDbContext<TestTenantEntityDbContext>(options => options.UseSqlite(connection))
            .AddScoped<DbContext>(provider => provider.GetRequiredService<TestTenantEntityDbContext>())
            .BuildServiceProvider();
        var requestedTenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();

        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TestTenantEntityDbContext>();
            await db.Database.EnsureCreatedAsync();
            db.AddRange(
                new TestTenantEntity { Id = Guid.NewGuid(), TenantId = requestedTenantId, Name = "Requested" },
                new TestTenantEntity { Id = Guid.NewGuid(), TenantId = otherTenantId, Name = "Other" });
            await db.SaveChangesAsync();
        }

        var service = new QueryExecutionService(
            services,
            NullLogger<QueryExecutionService>.Instance,
            CreateTrustedContext(requestedTenantId));
        service.RegisterEntity<TestTenantEntity>("TestTenantEntity");
        var descriptor = new QueryDescriptor
        {
            EntityTypeName = "TestTenantEntity",
            Mode = QueryExecutionMode.ToList,
            Metadata = new RequestMetadata
            {
                RequestedTenantId = requestedTenantId,
                UserAgent = "Portal;xframework.ignoreQueryFilters"
            }
        };

        var resultBytes = await service.ExecuteAsync(MemoryPackSerializer.Serialize(descriptor));
        var rows = MemoryPackSerializer.Deserialize<List<TestTenantEntity>>(resultBytes);

        rows.Should().ContainSingle().Which.TenantId.Should().Be(requestedTenantId);
    }

    [Test]
    public async Task ExecuteAsync_GlobalTenantReference_IncludesGlobalAndCurrentTenantRowsOnly()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var services = new ServiceCollection()
            .AddDbContext<TestTenantEntityDbContext>(options => options.UseSqlite(connection))
            .AddScoped<DbContext>(provider => provider.GetRequiredService<TestTenantEntityDbContext>())
            .BuildServiceProvider();
        var requestedTenantId = Guid.NewGuid();

        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TestTenantEntityDbContext>();
            await db.Database.EnsureCreatedAsync();
            db.GlobalTenantEntities.AddRange(
                new TestGlobalTenantEntity { Id = Guid.NewGuid(), TenantId = Guid.Empty, Name = "Global" },
                new TestGlobalTenantEntity { Id = Guid.NewGuid(), TenantId = requestedTenantId, Name = "Current" },
                new TestGlobalTenantEntity { Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), Name = "Foreign" });
            await db.SaveChangesAsync();
        }

        var service = new QueryExecutionService(
            services,
            NullLogger<QueryExecutionService>.Instance,
            CreateTrustedContext(requestedTenantId));
        service.RegisterEntity<TestGlobalTenantEntity>(nameof(TestGlobalTenantEntity));
        var descriptor = new QueryDescriptor
        {
            EntityTypeName = nameof(TestGlobalTenantEntity),
            Mode = QueryExecutionMode.ToList,
            Metadata = new RequestMetadata { RequestedTenantId = requestedTenantId }
        };

        var resultBytes = await service.ExecuteAsync(MemoryPackSerializer.Serialize(descriptor));
        var rows = MemoryPackSerializer.Deserialize<List<TestGlobalTenantEntity>>(resultBytes);

        rows.Should().NotBeNull();
        rows!.Select(row => row.Name).Should().BeEquivalentTo(new[] { "Global", "Current" });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task RemoteIdentityOperation_DisabledTargetFeature_ReturnsFailure(bool mutation)
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var tenantId = Guid.NewGuid();
        var featureService = new Mock<ITenantModuleFeatureService>(MockBehavior.Strict);
        featureService.Setup(service => service.EnsureEnabledAsync(
                tenantId,
                TenantModuleFeatureKeys.Identity,
                TenantModuleFeatureKeys.ContactsSubFeature,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Forbidden("disabled"));

        var services = new ServiceCollection()
            .AddDbContext<QueryExecutionDbContext>(options => options.UseSqlite(connection))
            .AddScoped<DbContext>(provider => provider.GetRequiredService<QueryExecutionDbContext>())
            .AddSingleton(featureService.Object)
            .BuildServiceProvider();
        await using (var scope = services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<DbContext>().Database.EnsureCreatedAsync();

        var service = new QueryExecutionService(
            services,
            NullLogger<QueryExecutionService>.Instance,
            CreateTrustedContext(tenantId));
        service.RegisterEntity(typeof(IdentityContact), nameof(IdentityContact), allowRemoteMutation: true);
        byte[] resultBytes;
        if (mutation)
        {
            var request = new SaveChangesRequest
            {
                Metadata = new RequestMetadata { RequestedTenantId = tenantId },
                Changes =
                [
                    new ChangeEntry
                    {
                        EntityTypeName = nameof(IdentityContact),
                        Operation = ChangeOperation.Add,
                        SerializedEntity = MemoryPackSerializer.Serialize(new IdentityContact
                        {
                            Id = Guid.NewGuid(),
                            TenantId = tenantId,
                            CredentialId = Guid.NewGuid(),
                            GroupId = Guid.NewGuid(),
                            Value = "test@example.com"
                        })
                    }
                ]
            };
            resultBytes = await service.ExecuteChangesAsync(MemoryPackSerializer.Serialize(request));
        }
        else
        {
            var descriptor = new QueryDescriptor
            {
                EntityTypeName = nameof(IdentityContact),
                Mode = QueryExecutionMode.ToList,
                Metadata = new RequestMetadata { RequestedTenantId = tenantId }
            };
            resultBytes = await service.ExecuteAsync(MemoryPackSerializer.Serialize(descriptor));
        }

        var result = MemoryPackSerializer.Deserialize<DataContextResult>(resultBytes);
        result.Should().NotBeNull();
        result!.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("The requested tenant feature is not enabled.");
        featureService.VerifyAll();
    }

    [Test]
    public async Task ExecuteChangesAsync_InvalidIdentityEntity_IsRejectedBeforePersistence()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var tenantId = Guid.NewGuid();
        var featureService = new Mock<ITenantModuleFeatureService>(MockBehavior.Strict);
        featureService.Setup(service => service.EnsureEnabledAsync(
                tenantId,
                TenantModuleFeatureKeys.Identity,
                TenantModuleFeatureKeys.ContactsSubFeature,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        var validator = new Mock<IRemoteDataContextEntityValidator>(MockBehavior.Strict);
        validator.SetupGet(candidate => candidate.EntityType).Returns(typeof(IdentityContact));
        validator.Setup(candidate => candidate.ValidateAsync(
                It.IsAny<object>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(["Value is required."]);

        var services = new ServiceCollection()
            .AddDbContext<QueryExecutionDbContext>(options => options.UseSqlite(connection))
            .AddScoped<DbContext>(provider => provider.GetRequiredService<QueryExecutionDbContext>())
            .AddSingleton(featureService.Object)
            .AddSingleton(validator.Object)
            .BuildServiceProvider();
        await using (var scope = services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<DbContext>().Database.EnsureCreatedAsync();

        var service = new QueryExecutionService(
            services,
            NullLogger<QueryExecutionService>.Instance,
            CreateTrustedContext(tenantId));
        service.RegisterEntity(typeof(IdentityContact), nameof(IdentityContact), allowRemoteMutation: true);
        var request = new SaveChangesRequest
        {
            Metadata = new RequestMetadata { RequestedTenantId = tenantId },
            Changes =
            [
                new ChangeEntry
                {
                    EntityTypeName = nameof(IdentityContact),
                    Operation = ChangeOperation.Add,
                    SerializedEntity = MemoryPackSerializer.Serialize(new IdentityContact
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        CredentialId = Guid.NewGuid(),
                        GroupId = Guid.NewGuid(),
                        Value = string.Empty
                    })
                }
            ]
        };

        var resultBytes = await service.ExecuteChangesAsync(MemoryPackSerializer.Serialize(request));
        var result = MemoryPackSerializer.Deserialize<DataContextResult>(resultBytes);

        result.Should().NotBeNull();
        result!.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Entity validation failed: Value is required.");
        featureService.VerifyAll();
        validator.VerifyAll();
    }

    [Test]
    public async Task ExecuteAsync_QueryFailure_ReturnsGenericErrorAndLogsDetails()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var tenantId = Guid.NewGuid();
        var featureService = CreateEnabledTenantFeatureService(tenantId);
        var services = new ServiceCollection()
            .AddDbContext<QueryExecutionDbContext>(options => options.UseSqlite(connection))
            .AddScoped<DbContext>(provider => provider.GetRequiredService<QueryExecutionDbContext>())
            .AddSingleton(featureService.Object)
            .BuildServiceProvider();
        await using (var scope = services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<DbContext>().Database.EnsureCreatedAsync();

        var logger = new Mock<ILogger<QueryExecutionService>>();
        var service = new QueryExecutionService(services, logger.Object, CreateTrustedContext(tenantId));
        service.RegisterEntity<Tenant>(nameof(Tenant));
        var descriptor = new QueryDescriptor
        {
            EntityTypeName = nameof(Tenant),
            Mode = QueryExecutionMode.ToList,
            Filters =
            [
                new QueryFilter
                {
                    PropertyName = "InternalColumnThatDoesNotExist",
                    Operation = QueryFilterOperation.Equal,
                    Value = "secret"
                }
            ]
        };

        var resultBytes = await service.ExecuteAsync(MemoryPackSerializer.Serialize(descriptor));
        var result = MemoryPackSerializer.Deserialize<DataContextResult>(resultBytes);

        result.Should().NotBeNull();
        result!.Message.Should().Be("The requested query could not be completed.");
        result.Message.Should().NotContain("InternalColumnThatDoesNotExist");
        logger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((_, _) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Test]
    public async Task ExecuteChangesAsync_StaleFieldPatch_ReturnsConflict()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var services = new ServiceCollection()
            .AddDbContext<TestTenantEntityDbContext>(options => options.UseSqlite(connection))
            .AddScoped<DbContext>(provider => provider.GetRequiredService<TestTenantEntityDbContext>())
            .BuildServiceProvider();
        var tenantId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        var originalStamp = Guid.NewGuid();
        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TestTenantEntityDbContext>();
            await db.Database.EnsureCreatedAsync();
            db.Add(new TestTenantEntity
            {
                Id = entityId,
                TenantId = tenantId,
                Name = "Original",
                ConcurrencyStamp = originalStamp
            });
            await db.SaveChangesAsync();
        }

        var service = new QueryExecutionService(
            services,
            NullLogger<QueryExecutionService>.Instance,
            CreateTrustedContext(tenantId));
        service.RegisterEntity(typeof(TestTenantEntity), "TestTenantEntity", allowRemoteMutation: true);

        async Task<DataContextResult> PatchName(string name)
        {
            var patch = new FieldPatch
            {
                EntityId = MemoryPackSerializer.Serialize(entityId),
                ExpectedConcurrencyStamp = originalStamp,
                Changes = new Dictionary<string, byte[]>
                {
                    [nameof(TestTenantEntity.Name)] = MemoryPackSerializer.Serialize<string?>(name)
                }
            };
            var request = new SaveChangesRequest
            {
                Metadata = new RequestMetadata { RequestedTenantId = tenantId },
                Changes =
                [
                    new ChangeEntry
                    {
                        EntityTypeName = "TestTenantEntity",
                        Operation = ChangeOperation.Update,
                        SerializedEntity = MemoryPackSerializer.Serialize(patch)
                    }
                ]
            };
            var bytes = await service.ExecuteChangesAsync(MemoryPackSerializer.Serialize(request));
            return MemoryPackSerializer.Deserialize<DataContextResult>(bytes)!;
        }

        (await PatchName("First writer")).IsSuccess.Should().BeTrue();
        var staleResult = await PatchName("Stale writer");
        staleResult.IsSuccess.Should().BeFalse();
        staleResult.StatusCode.Should().Be(409);

        await using var verifyScope = services.CreateAsyncScope();
        var saved = await verifyScope.ServiceProvider.GetRequiredService<TestTenantEntityDbContext>()
            .TestTenantEntities.SingleAsync();
        saved.Name.Should().Be("First writer");
    }

    [TestCase(nameof(BaseModel.IsDeleted))]
    [TestCase(nameof(BaseModel.DeletedAt))]
    [TestCase(nameof(BaseModel.CreatedAt))]
    [TestCase(nameof(BaseModel.ModifiedAt))]
    [TestCase(nameof(BaseModel.IsEnabled))]
    [TestCase(nameof(BaseModel.ConcurrencyStamp))]
    [TestCase(nameof(BaseModel.TenantId))]
    [TestCase(nameof(BaseModel.Id))]
    public async Task ExecuteChangesAsync_ProtectedFieldPatch_IsRejected(string propertyName)
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var services = new ServiceCollection()
            .AddDbContext<TestTenantEntityDbContext>(options => options.UseSqlite(connection))
            .AddScoped<DbContext>(provider => provider.GetRequiredService<TestTenantEntityDbContext>())
            .BuildServiceProvider();
        var tenantId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        var stamp = Guid.NewGuid();
        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TestTenantEntityDbContext>();
            await db.Database.EnsureCreatedAsync();
            db.Add(new TestTenantEntity
            {
                Id = entityId,
                TenantId = tenantId,
                Name = "Original",
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                ConcurrencyStamp = stamp
            });
            await db.SaveChangesAsync();
        }

        var property = typeof(TestTenantEntity).GetProperty(propertyName)!;
        var value = propertyName switch
        {
            nameof(BaseModel.IsDeleted) or nameof(BaseModel.IsEnabled) => MemoryPackSerializer.Serialize(false),
            nameof(BaseModel.DeletedAt) or nameof(BaseModel.ModifiedAt) =>
                MemoryPackSerializer.Serialize<DateTime?>(DateTime.UtcNow),
            nameof(BaseModel.CreatedAt) => MemoryPackSerializer.Serialize(DateTime.UtcNow.AddDays(-1)),
            _ => MemoryPackSerializer.Serialize(Guid.NewGuid())
        };
        var patch = new FieldPatch
        {
            EntityId = MemoryPackSerializer.Serialize(entityId),
            ExpectedConcurrencyStamp = stamp,
            Changes = new Dictionary<string, byte[]> { [property.Name] = value }
        };
        var request = new SaveChangesRequest
        {
            Metadata = new RequestMetadata { RequestedTenantId = tenantId },
            Changes =
            [
                new ChangeEntry
                {
                    EntityTypeName = nameof(TestTenantEntity),
                    Operation = ChangeOperation.Update,
                    SerializedEntity = MemoryPackSerializer.Serialize(patch)
                }
            ]
        };

        var service = new QueryExecutionService(
            services,
            NullLogger<QueryExecutionService>.Instance,
            CreateTrustedContext(tenantId));
        service.RegisterEntity(typeof(TestTenantEntity), nameof(TestTenantEntity), allowRemoteMutation: true);
        var bytes = await service.ExecuteChangesAsync(MemoryPackSerializer.Serialize(request));
        var result = MemoryPackSerializer.Deserialize<DataContextResult>(bytes)!;

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("cannot be changed through remote DataContext");
        await using var verifyScope = services.CreateAsyncScope();
        var saved = await verifyScope.ServiceProvider.GetRequiredService<TestTenantEntityDbContext>()
            .TestTenantEntities.AsNoTracking().SingleAsync();
        saved.IsDeleted.Should().BeFalse();
        saved.IsEnabled.Should().BeTrue();
        saved.TenantId.Should().Be(tenantId);
        saved.ConcurrencyStamp.Should().Be(stamp);
    }

    [Test]
    public async Task ExecuteChangesAsync_FullEntityUpdateFallback_IsRejected()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var services = new ServiceCollection()
            .AddDbContext<TestTenantEntityDbContext>(options => options.UseSqlite(connection))
            .AddScoped<DbContext>(provider => provider.GetRequiredService<TestTenantEntityDbContext>())
            .BuildServiceProvider();
        var tenantId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TestTenantEntityDbContext>();
            await db.Database.EnsureCreatedAsync();
            db.Add(new TestTenantEntity { Id = entityId, TenantId = tenantId, Name = "Original" });
            await db.SaveChangesAsync();
        }

        var request = new SaveChangesRequest
        {
            Metadata = new RequestMetadata { RequestedTenantId = tenantId },
            Changes =
            [
                new ChangeEntry
                {
                    EntityTypeName = nameof(TestTenantEntity),
                    Operation = ChangeOperation.Update,
                    SerializedEntity = MemoryPackSerializer.Serialize(new TestTenantEntity
                    {
                        Id = entityId,
                        TenantId = tenantId,
                        Name = "Bypass",
                        IsDeleted = true
                    })
                }
            ]
        };
        var service = new QueryExecutionService(
            services,
            NullLogger<QueryExecutionService>.Instance,
            CreateTrustedContext(tenantId));
        service.RegisterEntity(typeof(TestTenantEntity), nameof(TestTenantEntity), allowRemoteMutation: true);

        var bytes = await service.ExecuteChangesAsync(MemoryPackSerializer.Serialize(request));
        var result = MemoryPackSerializer.Deserialize<DataContextResult>(bytes)!;

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("require a field patch");
        await using var verifyScope = services.CreateAsyncScope();
        var saved = await verifyScope.ServiceProvider.GetRequiredService<TestTenantEntityDbContext>()
            .TestTenantEntities.AsNoTracking().SingleAsync();
        saved.Name.Should().Be("Original");
        saved.IsDeleted.Should().BeFalse();
    }

    [Test]
    public async Task ExecuteChangesAsync_TenantOwnedEntityWithMismatchedTrustedTenant_ReturnsFailureAndDoesNotSave()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var services = new ServiceCollection()
            .AddDbContext<TestTenantEntityDbContext>(options => options.UseSqlite(connection))
            .AddScoped<DbContext>(provider => provider.GetRequiredService<TestTenantEntityDbContext>())
            .BuildServiceProvider();

        await using (var setupScope = services.CreateAsyncScope())
        {
            var setupDbContext = setupScope.ServiceProvider.GetRequiredService<DbContext>();
            await setupDbContext.Database.EnsureCreatedAsync();
        }

        var entityTenantId = Guid.NewGuid();
        var requestTenantId = Guid.NewGuid();
        var service = new QueryExecutionService(
            services,
            NullLogger<QueryExecutionService>.Instance,
            CreateTrustedContext(requestTenantId));
        service.RegisterEntity(typeof(TestTenantEntity), "TestTenantEntity", allowRemoteMutation: true);

        var request = new SaveChangesRequest
        {
            Metadata = new RequestMetadata { RequestedTenantId = requestTenantId },
            Changes =
            [
                new ChangeEntry
                {
                    EntityTypeName = "TestTenantEntity",
                    Operation = ChangeOperation.Add,
                    SerializedEntity = MemoryPackSerializer.Serialize(new TestTenantEntity
                    {
                        Id = Guid.NewGuid(),
                        TenantId = entityTenantId,
                        Name = "Spoofed tenant"
                    })
                }
            ]
        };

        var resultBytes = await service.ExecuteChangesAsync(MemoryPackSerializer.Serialize(request));
        var result = MemoryPackSerializer.Deserialize<DataContextResult>(resultBytes);

        result.Should().NotBeNull();
        result!.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("does not match the trusted invocation tenant");

        await using var verifyScope = services.CreateAsyncScope();
        var dbContext = verifyScope.ServiceProvider.GetRequiredService<TestTenantEntityDbContext>();
        (await dbContext.TestTenantEntities.CountAsync()).Should().Be(0);
    }

    [Test]
    public async Task ExecuteChangesAsync_DatabaseConstraintFailure_ReturnsGenericErrorAndLogsDetails()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var services = new ServiceCollection()
            .AddDbContext<TestTenantEntityDbContext>(options => options.UseSqlite(connection))
            .AddScoped<DbContext>(provider => provider.GetRequiredService<TestTenantEntityDbContext>())
            .BuildServiceProvider();

        var tenantId = Guid.NewGuid();
        var duplicateId = Guid.NewGuid();
        await using (var setupScope = services.CreateAsyncScope())
        {
            var setupDbContext = setupScope.ServiceProvider.GetRequiredService<TestTenantEntityDbContext>();
            await setupDbContext.Database.EnsureCreatedAsync();
            setupDbContext.TestTenantEntities.Add(new TestTenantEntity
            {
                Id = duplicateId,
                TenantId = tenantId,
                Name = "Existing"
            });
            await setupDbContext.SaveChangesAsync();
        }

        var logger = new Mock<ILogger<QueryExecutionService>>();
        var service = new QueryExecutionService(
            services,
            logger.Object,
            CreateTrustedContext(tenantId));
        service.RegisterEntity(typeof(TestTenantEntity), "TestTenantEntity", allowRemoteMutation: true);

        var request = new SaveChangesRequest
        {
            Metadata = new RequestMetadata { RequestedTenantId = tenantId },
            Changes =
            [
                CreateAddChange(duplicateId, tenantId, "Duplicate")
            ]
        };

        var resultBytes = await service.ExecuteChangesAsync(MemoryPackSerializer.Serialize(request));
        var result = MemoryPackSerializer.Deserialize<DataContextResult>(resultBytes);

        result.Should().NotBeNull();
        result!.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("The requested data change conflicts with existing data or constraints.");
        result.Message.Should().NotContain("UNIQUE");
        result.Message.Should().NotContain("TestTenantEntities");
        logger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((_, _) => true),
                It.IsAny<DbUpdateException>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Test]
    public async Task ExecuteChangesAsync_QueryOnlyEntityRegistration_ReturnsFailureAndDoesNotSave()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var services = new ServiceCollection()
            .AddDbContext<TestTenantEntityDbContext>(options => options.UseSqlite(connection))
            .AddScoped<DbContext>(provider => provider.GetRequiredService<TestTenantEntityDbContext>())
            .BuildServiceProvider();

        await using (var setupScope = services.CreateAsyncScope())
        {
            var setupDbContext = setupScope.ServiceProvider.GetRequiredService<DbContext>();
            await setupDbContext.Database.EnsureCreatedAsync();
        }

        var tenantId = Guid.NewGuid();
        var service = new QueryExecutionService(
            services,
            NullLogger<QueryExecutionService>.Instance,
            CreateTrustedContext(tenantId));
        service.RegisterEntity(typeof(TestTenantEntity), "TestTenantEntity", allowRemoteMutation: false);

        var request = new SaveChangesRequest
        {
            Metadata = new RequestMetadata { RequestedTenantId = tenantId },
            Changes =
            [
                new ChangeEntry
                {
                    EntityTypeName = "TestTenantEntity",
                    Operation = ChangeOperation.Add,
                    SerializedEntity = MemoryPackSerializer.Serialize(new TestTenantEntity
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        Name = "Query only"
                    })
                }
            ]
        };

        var resultBytes = await service.ExecuteChangesAsync(MemoryPackSerializer.Serialize(request));
        var result = MemoryPackSerializer.Deserialize<DataContextResult>(resultBytes);

        result.Should().NotBeNull();
        result!.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("not registered for remote mutation");

        await using var verifyScope = services.CreateAsyncScope();
        var dbContext = verifyScope.ServiceProvider.GetRequiredService<TestTenantEntityDbContext>();
        (await dbContext.TestTenantEntities.CountAsync()).Should().Be(0);
    }

    [Test]
    public void ValidateDescriptor_OversizedFilterCollections_AreRejected()
    {
        var filters = Enumerable.Range(0, QueryDescriptorExecutor.MaximumFilterCount + 1)
            .Select(index => new QueryFilter
            {
                PropertyName = nameof(TestTenantEntity.Name),
                Operation = QueryFilterOperation.Equal,
                Value = index.ToString()
            })
            .ToList();
        var descriptor = new QueryDescriptor { Filters = filters };

        QueryDescriptorExecutor.ValidateDescriptor(descriptor)
            .Should().Contain($"at most {QueryDescriptorExecutor.MaximumFilterCount} filters");
    }

    [Test]
    public void ValidateDescriptor_ExcessiveOffset_IsRejected()
    {
        var descriptor = new QueryDescriptor
        {
            Skip = QueryDescriptorExecutor.MaximumSkip + 1,
            Take = 1
        };

        QueryDescriptorExecutor.ValidateDescriptor(descriptor)
            .Should().Contain($"between 0 and {QueryDescriptorExecutor.MaximumSkip}");
    }

    [Test]
    public void ValidateDescriptor_OversizedSortingAndPredicateCollections_AreRejected()
    {
        var sorting = Enumerable.Range(0, QueryDescriptorExecutor.MaximumSortCount + 1)
            .Select(_ => new SortDescriptor { PropertyName = nameof(TestTenantEntity.Name) })
            .ToList();
        var descriptor = new QueryDescriptor { Sorting = sorting };

        QueryDescriptorExecutor.ValidateDescriptor(descriptor)
            .Should().Contain($"at most {QueryDescriptorExecutor.MaximumSortCount} sort fields");

        descriptor.Sorting = [];
        descriptor.PredicateFilters = Enumerable.Range(0, QueryDescriptorExecutor.MaximumPredicateFilterCount + 1)
            .Select(index => new QueryFilter
            {
                PropertyName = nameof(TestTenantEntity.Name),
                Operation = QueryFilterOperation.Equal,
                Value = index.ToString()
            })
            .ToList();

        QueryDescriptorExecutor.ValidateDescriptor(descriptor)
            .Should().Contain($"at most {QueryDescriptorExecutor.MaximumPredicateFilterCount} predicate filters");
    }

    [Test]
    public void ValidateDescriptor_OversizedInList_IsRejectedIndependentlyOfTotalFilterLimit()
    {
        var descriptor = new QueryDescriptor
        {
            Filters = Enumerable.Range(0, QueryDescriptorExecutor.MaximumInValueCount + 1)
                .Select(index => new QueryFilter
                {
                    PropertyName = nameof(TestTenantEntity.Name),
                    Operation = QueryFilterOperation.In,
                    Value = index.ToString()
                })
                .ToList()
        };

        QueryDescriptorExecutor.ValidateDescriptor(descriptor)
            .Should().Contain($"at most {QueryDescriptorExecutor.MaximumInValueCount} values");
    }

    [Test]
    public async Task ExecuteChangesAsync_OversizedMutationBatch_IsRejectedBeforeServiceResolution()
    {
        var request = new SaveChangesRequest
        {
            Changes = Enumerable.Range(0, QueryExecutionService.MaximumMutationBatchSize + 1)
                .Select(_ => new ChangeEntry
                {
                    EntityTypeName = nameof(TestTenantEntity),
                    Operation = ChangeOperation.Add,
                    SerializedEntity = [0]
                })
                .ToList()
        };
        var service = new QueryExecutionService(
            new ServiceCollection().BuildServiceProvider(),
            NullLogger<QueryExecutionService>.Instance,
            CreateTrustedContext());

        var bytes = await service.ExecuteChangesAsync(MemoryPackSerializer.Serialize(request));
        var result = MemoryPackSerializer.Deserialize<DataContextResult>(bytes)!;

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain($"at most {QueryExecutionService.MaximumMutationBatchSize} changes");
    }

    [Test]
    public async Task ExecuteChangesAsync_OversizedSerializedEntity_IsRejectedBeforeEntityDeserialization()
    {
        var request = new SaveChangesRequest
        {
            Changes =
            [
                new ChangeEntry
                {
                    EntityTypeName = nameof(TestTenantEntity),
                    Operation = ChangeOperation.Add,
                    SerializedEntity = new byte[QueryExecutionService.MaximumSerializedEntitySizeBytes + 1]
                }
            ]
        };
        var service = new QueryExecutionService(
            new ServiceCollection().BuildServiceProvider(),
            NullLogger<QueryExecutionService>.Instance,
            CreateTrustedContext());

        var bytes = await service.ExecuteChangesAsync(MemoryPackSerializer.Serialize(request));
        var result = MemoryPackSerializer.Deserialize<DataContextResult>(bytes)!;

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain($"must not exceed {QueryExecutionService.MaximumSerializedEntitySizeBytes} bytes");
    }

    [Test]
    public async Task ExecuteChangesAsync_OversizedRequest_IsRejectedBeforeRequestDeserialization()
    {
        var service = new QueryExecutionService(
            new ServiceCollection().BuildServiceProvider(),
            NullLogger<QueryExecutionService>.Instance,
            CreateTrustedContext());

        var bytes = await service.ExecuteChangesAsync(
            new byte[QueryExecutionService.MaximumSaveChangesRequestSizeBytes + 1]);
        var result = MemoryPackSerializer.Deserialize<DataContextResult>(bytes)!;

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain($"must not exceed {QueryExecutionService.MaximumSaveChangesRequestSizeBytes} bytes");
    }

    [Test]
    public async Task ExecuteAsync_OversizedDescriptor_IsRejectedBeforeDescriptorDeserialization()
    {
        var service = new QueryExecutionService(
            new ServiceCollection().BuildServiceProvider(),
            NullLogger<QueryExecutionService>.Instance,
            CreateTrustedContext());

        var bytes = await service.ExecuteAsync(
            new byte[QueryDescriptorExecutor.MaximumQueryDescriptorBytes + 1]);
        var result = MemoryPackSerializer.Deserialize<DataContextResult>(bytes)!;

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain($"must not exceed {QueryDescriptorExecutor.MaximumQueryDescriptorBytes} bytes");
    }

    private sealed class QueryExecutionDbContext(DbContextOptions<QueryExecutionDbContext> options)
        : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var tenant = modelBuilder.Entity<Tenant>();
            tenant.HasKey(x => x.Id);
            tenant.Ignore(x => x.IdentityCredentials);
            tenant.Ignore(x => x.IdentityInformations);
            tenant.Ignore(x => x.IdentityRoleTypes);
            tenant.Ignore(x => x.RegistryConfigurations);

            var feature = modelBuilder.Entity<TenantModuleFeature>();
            feature.HasKey(x => x.Id);
            feature.Ignore(x => x.Tenant);
        }
    }

    private static Tenant CreateTenant(Guid tenantId, string name) => new()
    {
        Id = tenantId,
        TenantId = tenantId,
        Name = name,
        IsEnabled = true,
        CreatedAt = DateTime.UtcNow,
        ConcurrencyStamp = Guid.NewGuid()
    };

    private static TenantModuleFeature CreateTenantModuleFeature(Guid tenantId, string displayName) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        ModuleKey = TenantModuleFeatureKeys.Identity,
        SubFeatureKey = TenantModuleFeatureKeys.UsersSubFeature,
        DisplayName = displayName,
        IsEnabled = true,
        CreatedAt = DateTime.UtcNow,
        ConcurrencyStamp = Guid.NewGuid()
    };

    private static Mock<ITenantModuleFeatureService> CreateEnabledTenantFeatureService(Guid tenantId)
    {
        var featureService = new Mock<ITenantModuleFeatureService>(MockBehavior.Strict);
        featureService.Setup(service => service.EnsureEnabledAsync(
                tenantId,
                TenantModuleFeatureKeys.Identity,
                TenantModuleFeatureKeys.TenantsSubFeature,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        return featureService;
    }

    private static ChangeEntry CreateAddChange(Guid id, Guid tenantId, string name) =>
        new()
        {
            EntityTypeName = "TestTenantEntity",
            Operation = ChangeOperation.Add,
            SerializedEntity = MemoryPackSerializer.Serialize(new TestTenantEntity
            {
                Id = id,
                TenantId = tenantId,
                Name = name
            })
        };

    private static ITrustedInvocationContextAccessor CreateTrustedContext(
        Guid? effectiveTenantId = null,
        TrustedActorIdentity? actor = null,
        IReadOnlySet<string>? serviceScopes = null,
        string serviceClientId = XFrameworkServiceNames.Portal) =>
        new TestTrustedInvocationContextAccessor(new TrustedInvocationContext(
            actor,
            new TrustedServiceIdentity(
                serviceClientId,
                "core-tests",
                serviceScopes ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    XFrameworkServiceScopes.DataContextQuery,
                    XFrameworkServiceScopes.DataContextQueryAllTenants,
                    XFrameworkServiceScopes.DataContextMutate,
                    XFrameworkServiceScopes.TenantTarget
                },
                "test-service-generation"),
            effectiveTenantId,
            effectiveTenantId,
            Guid.NewGuid()));

    private static TrustedActorIdentity CreateActor(Guid tenantId, Guid credentialId) =>
        new(
            credentialId,
            Guid.NewGuid(),
            tenantId,
            Guid.NewGuid(),
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            "test-actor-generation",
            DateTimeOffset.UtcNow.AddMinutes(5));

    private sealed class TestTrustedInvocationContextAccessor(TrustedInvocationContext current)
        : ITrustedInvocationContextAccessor
    {
        public TrustedInvocationContext? Current => current;
    }

    [MemoryPackable]
    public partial class TestTenantEntity : BaseModel
    {
        [MemoryPackOrder(0)]
        public string? Name { get; set; }
    }

    [MemoryPackable]
    public partial class TestGlobalTenantEntity : BaseModel, IAllowsGlobalTenantRows
    {
        [MemoryPackOrder(0)]
        public string? Name { get; set; }
    }

    private sealed class TestTenantEntityDbContext(DbContextOptions<TestTenantEntityDbContext> options)
        : DbContext(options)
    {
        public DbSet<TestTenantEntity> TestTenantEntities => Set<TestTenantEntity>();
        public DbSet<TestGlobalTenantEntity> GlobalTenantEntities => Set<TestGlobalTenantEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TestTenantEntity>().HasKey(x => x.Id);
            modelBuilder.Entity<TestTenantEntity>().Property(x => x.ConcurrencyStamp).IsConcurrencyToken();
            modelBuilder.Entity<TestGlobalTenantEntity>().HasKey(x => x.Id);
        }
    }

    private sealed class FilteredQueryExecutionDbContext(
        DbContextOptions<FilteredQueryExecutionDbContext> options,
        IHttpContextAccessor httpContextAccessor,
        IConfiguration configuration,
        IEffectiveTenantContextAccessor tenantContextAccessor,
        ICrossTenantWriteAuthorizationAccessor crossTenantWriteAuthorizationAccessor)
        : XDbContext(
            options,
            httpContextAccessor,
            configuration,
            tenantContextAccessor,
            crossTenantWriteAuthorizationAccessor)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var tenant = modelBuilder.Entity<Tenant>();
            tenant.HasKey(x => x.Id);
            tenant.Ignore(x => x.IdentityCredentials);
            tenant.Ignore(x => x.IdentityInformations);
            tenant.Ignore(x => x.IdentityRoleTypes);
            tenant.Ignore(x => x.RegistryConfigurations);

            base.OnModelCreating(modelBuilder);
        }
    }

    private sealed class TestEffectiveTenantContextAccessor(Guid tenantId)
        : IEffectiveTenantContextAccessor
    {
        public bool HasTrustedInvocation => true;
        public Guid? EffectiveTenantId => tenantId;
    }

    private sealed class TestCrossTenantWriteAuthorizationAccessor
        : ICrossTenantWriteAuthorizationAccessor
    {
        public bool IsAuthorized => true;
    }
}
