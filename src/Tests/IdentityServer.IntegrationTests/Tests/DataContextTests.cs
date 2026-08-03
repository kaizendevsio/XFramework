using IdentityServer.Domain.Shared;
using IdentityServer.Domain.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using XFramework.Domain.Contexts;
using XFramework.Domain.Shared.Contracts.Base;
using XFramework.Domain.Shared.DataContext;
using XFramework.TestInfrastructure;

namespace IdentityServer.IntegrationTests;

/// <summary>
/// End-to-end tests for RemoteDataContext: test client -> Bolt Hub -> IdentityServer -> DbContext -> PostgreSQL.
/// </summary>
[TestFixture]
[Category(TestCategories.Integration)]
[Category(TestCategories.ExtendedIntegration)]
[Category(TestCategories.IdentityServer)]
[Category(TestCategories.DataContext)]
public class DataContextTests
{
    [Test]
    public async Task RemoteQuery_ToListAsync_ReturnsEntitiesFromService()
    {
        var results = await IntegrationTestFixture.DataContext.Query<Tenant>()
            .Where(t => t.Id == IntegrationTestFixture.TestTenantId)
            .ToListAsync();

        results.Should().NotBeEmpty();
        results[0].Id.Should().Be(IntegrationTestFixture.TestTenantId);
    }

    [Test]
    public async Task RemoteQuery_FirstOrDefaultAsync_ReturnsSingleEntity()
    {
        var result = await IntegrationTestFixture.DataContext.Query<Tenant>()
            .Where(t => t.Id == IntegrationTestFixture.TestTenantId)
            .FirstOrDefaultAsync();

        result.Should().NotBeNull();
        result!.Id.Should().Be(IntegrationTestFixture.TestTenantId);
    }

    [Test]
    public async Task RemoteQuery_CountAsync_ReturnsCount()
    {
        var count = await IntegrationTestFixture.DataContext.Query<Tenant>()
            .Where(t => t.Id == IntegrationTestFixture.TestTenantId)
            .CountAsync();

        count.Should().BeGreaterThan(0);
    }

    [Test]
    public async Task RemoteQuery_AnyAsync_ReturnsTrue()
    {
        var exists = await IntegrationTestFixture.DataContext.Query<Tenant>()
            .AnyAsync();

        exists.Should().BeTrue();
    }

    [Test]
    public async Task RemoteMutation_IdentityContact_CreateUpdateRemoveAndTenantMismatch_AreEnforced()
    {
        var seed = await SeedMutationDependencies();
        var contact = new IdentityContact
        {
            Id = Guid.NewGuid(),
            TenantId = IntegrationTestFixture.TestTenantId,
            CredentialId = seed.CredentialId,
            GroupId = seed.ContactGroupId,
            Value = $"contact-{Guid.NewGuid():N}",
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow
        };

        var updatedValue = $"updated-{Guid.NewGuid():N}";
        await AssertRemoteLifecycle(
            contact,
            item => item.Value = updatedValue,
            item => item.Value.Should().Be(updatedValue));

        await AssertTenantMismatchRejected(new IdentityContact
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            CredentialId = seed.CredentialId,
            GroupId = seed.ContactGroupId,
            Value = $"mismatch-{Guid.NewGuid():N}",
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow
        });
    }

    [Test]
    public async Task RemoteMutation_IdentityAddress_CreateUpdateRemoveAndTenantMismatch_AreEnforced()
    {
        var seed = await SeedMutationDependencies();
        var address = new IdentityAddress
        {
            Id = Guid.NewGuid(),
            TenantId = IntegrationTestFixture.TestTenantId,
            IdentityInfoId = seed.IdentityId,
            Name = $"Address {Guid.NewGuid():N}",
            Street = "Initial Street",
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow
        };

        var updatedStreet = $"Updated Street {Guid.NewGuid():N}";
        await AssertRemoteLifecycle(
            address,
            item => item.Street = updatedStreet,
            item => item.Street.Should().Be(updatedStreet));

        await AssertTenantMismatchRejected(new IdentityAddress
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            IdentityInfoId = seed.IdentityId,
            Name = "Mismatched address",
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow
        });
    }

    [Test]
    public async Task RemoteMutation_IdentityFavorite_CreateUpdateRemoveAndTenantMismatch_AreEnforced()
    {
        var seed = await SeedMutationDependencies();
        var favorite = new IdentityFavorite
        {
            Id = Guid.NewGuid(),
            TenantId = IntegrationTestFixture.TestTenantId,
            CredentialId = seed.CredentialId,
            Data = $"Favorite {Guid.NewGuid():N}",
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow
        };

        var updatedData = $"Updated favorite {Guid.NewGuid():N}";
        await AssertRemoteLifecycle(
            favorite,
            item => item.Data = updatedData,
            item => item.Data.Should().Be(updatedData));

        await AssertTenantMismatchRejected(new IdentityFavorite
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            CredentialId = seed.CredentialId,
            Data = "Mismatched favorite",
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow
        });
    }

    [Test]
    public async Task RemoteMutation_RegistryConfiguration_CreateUpdateRemoveAndTenantMismatch_AreEnforced()
    {
        var seed = await SeedMutationDependencies();
        var configuration = new RegistryConfiguration
        {
            Id = Guid.NewGuid(),
            TenantId = IntegrationTestFixture.TestTenantId,
            GroupId = seed.RegistryGroupId,
            Key = $"integration.{Guid.NewGuid():N}",
            Value = "initial",
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow
        };

        await AssertRemoteLifecycle(
            configuration,
            item => item.Value = "updated",
            item => item.Value.Should().Be("updated"));

        await AssertTenantMismatchRejected(new RegistryConfiguration
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            GroupId = seed.RegistryGroupId,
            Key = $"mismatch.{Guid.NewGuid():N}",
            Value = "rejected",
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow
        });
    }

    [Test]
    public async Task RemoteMutation_UnannotatedIdentityInformation_IsRejectedAndNotPersisted()
    {
        var identityId = Guid.NewGuid();
        var context = IntegrationTestFixture.DataContext;
        context.Add(new IdentityInformation
        {
            Id = identityId,
            TenantId = IntegrationTestFixture.TestTenantId,
            IdentityName = "Rejected remote identity mutation",
            IsEnabled = true
        });

        var save = await context.SaveChangesAsync();

        save.IsSuccess.Should().BeFalse();
        await using var db = OpenDbContext();
        (await db.Set<IdentityInformation>().IgnoreQueryFilters().AnyAsync(x => x.Id == identityId))
            .Should().BeFalse();
    }

    [Test]
    public async Task RemoteMutation_CrossTenantRelationshipIds_AreRejected()
    {
        var local = await SeedMutationDependencies();
        var foreign = await SeedForeignTenantDependencies();

        await AssertRelationshipRejected(new IdentityAddress
        {
            Id = Guid.NewGuid(),
            TenantId = IntegrationTestFixture.TestTenantId,
            IdentityInfoId = foreign.IdentityId,
            Name = "Cross-tenant address",
            IsEnabled = true
        });
        await AssertRelationshipRejected(new IdentityContact
        {
            Id = Guid.NewGuid(),
            TenantId = IntegrationTestFixture.TestTenantId,
            CredentialId = foreign.CredentialId,
            GroupId = foreign.ContactGroupId,
            Value = "cross-tenant-contact",
            IsEnabled = true
        });
        await AssertRelationshipRejected(new IdentityContact
        {
            Id = Guid.NewGuid(),
            TenantId = IntegrationTestFixture.TestTenantId,
            CredentialId = local.CredentialId,
            GroupId = foreign.ContactGroupId,
            TypeId = IdentityConstants.ContactType.Email,
            Value = "cross-tenant-contact-group",
            IsEnabled = true
        });
        await AssertRelationshipRejected(new IdentityContact
        {
            Id = Guid.NewGuid(),
            TenantId = IntegrationTestFixture.TestTenantId,
            CredentialId = local.CredentialId,
            GroupId = IdentityConstants.ContactGroup.Personal,
            TypeId = foreign.ContactTypeId,
            Value = "cross-tenant-contact-type",
            IsEnabled = true
        });
        await AssertRelationshipRejected(new IdentityAddress
        {
            Id = Guid.NewGuid(),
            TenantId = IntegrationTestFixture.TestTenantId,
            IdentityInfoId = local.IdentityId,
            AddressTypeId = foreign.AddressTypeId,
            Name = "Cross-tenant address type",
            IsEnabled = true
        });
        await AssertRelationshipRejected(new IdentityFavorite
        {
            Id = Guid.NewGuid(),
            TenantId = IntegrationTestFixture.TestTenantId,
            CredentialId = foreign.CredentialId,
            Data = "cross-tenant-favorite",
            IsEnabled = true
        });
        await AssertRelationshipRejected(new RegistryConfiguration
        {
            Id = Guid.NewGuid(),
            TenantId = IntegrationTestFixture.TestTenantId,
            GroupId = foreign.RegistryGroupId,
            Key = $"cross-tenant.{Guid.NewGuid():N}",
            Value = "rejected",
            IsEnabled = true
        });
    }

    [Test]
    public async Task RemoteMutation_ApprovedGlobalSeededLookups_AreAccepted()
    {
        var seed = await SeedMutationDependencies();
        var contact = new IdentityContact
        {
            Id = Guid.NewGuid(),
            TenantId = IntegrationTestFixture.TestTenantId,
            CredentialId = seed.CredentialId,
            GroupId = IdentityConstants.ContactGroup.Personal,
            TypeId = IdentityConstants.ContactType.Email,
            Value = $"global-lookup-{Guid.NewGuid():N}@example.test",
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow
        };
        var address = new IdentityAddress
        {
            Id = Guid.NewGuid(),
            TenantId = IntegrationTestFixture.TestTenantId,
            IdentityInfoId = seed.IdentityId,
            AddressTypeId = IdentityConstants.AddressType.Home,
            Name = "Global address type",
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow
        };

        await AssertRemoteLifecycle(
            contact,
            item => item.Value = $"updated-{Guid.NewGuid():N}@example.test",
            item => item.TypeId.Should().Be(IdentityConstants.ContactType.Email));
        await AssertRemoteLifecycle(
            address,
            item => item.Name = "Updated global address type",
            item => item.AddressTypeId.Should().Be(IdentityConstants.AddressType.Home));
    }

    [Test]
    public async Task RemoteQuery_GlobalReferenceType_ReturnsGlobalAndCurrentTenantRowsOnly()
    {
        var localId = Guid.NewGuid();
        var foreignId = Guid.NewGuid();
        await using (var db = OpenDbContext())
        {
            db.Set<IdentityContactType>().AddRange(
                new IdentityContactType
                {
                    Id = localId,
                    TenantId = IntegrationTestFixture.TestTenantId,
                    Name = "Tenant contact type",
                    SystemReferenceId = Guid.NewGuid(),
                    IsEnabled = true,
                    CreatedAt = DateTime.UtcNow,
                    ConcurrencyStamp = Guid.NewGuid()
                },
                new IdentityContactType
                {
                    Id = foreignId,
                    TenantId = Guid.NewGuid(),
                    Name = "Foreign contact type",
                    SystemReferenceId = Guid.NewGuid(),
                    IsEnabled = true,
                    CreatedAt = DateTime.UtcNow,
                    ConcurrencyStamp = Guid.NewGuid()
                });
            await db.SaveChangesAsync();
        }

        var visible = await IntegrationTestFixture.DataContext.Query<IdentityContactType>()
            .Where(item => item.Id == IdentityConstants.ContactType.Email ||
                           item.Id == localId ||
                           item.Id == foreignId)
            .ToListAsync();

        visible.Select(item => item.Id).Should().BeEquivalentTo(
            new[] { IdentityConstants.ContactType.Email, localId });
    }

    [Test]
    public async Task RemoteMutation_RegistryConfigurationGroup_CanBootstrapSimpleTenantOwnedGroup()
    {
        var group = new RegistryConfigurationGroup
        {
            Id = Guid.NewGuid(),
            TenantId = IntegrationTestFixture.TestTenantId,
            Name = "Community",
            Description = "Community portal settings",
            SystemReferenceId = Guid.NewGuid(),
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow
        };

        await AssertRemoteLifecycle(
            group,
            item => item.Description = "Updated community portal settings",
            item => item.Description.Should().Be("Updated community portal settings"));
    }

    [Test]
    public async Task RemoteMutation_RegistryConfigurationGroup_ForeignTenantCreateUpdateAndRemoveAreRejected()
    {
        var foreignTenantId = Guid.NewGuid();
        await AssertTenantMismatchRejected(new RegistryConfigurationGroup
        {
            Id = Guid.NewGuid(),
            TenantId = foreignTenantId,
            Name = $"Foreign create {Guid.NewGuid():N}",
            SystemReferenceId = Guid.NewGuid(),
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            ConcurrencyStamp = Guid.NewGuid()
        });

        var updateGroup = CreateForeignRegistryGroup(foreignTenantId, "update");
        var removeGroup = CreateForeignRegistryGroup(foreignTenantId, "remove");
        await using (var db = OpenDbContext())
        {
            db.Set<RegistryConfigurationGroup>().AddRange(updateGroup, removeGroup);
            await db.SaveChangesAsync();
        }

        updateGroup.Description = "Unauthorized update";
        var updateContext = IntegrationTestFixture.DataContext;
        updateContext.Update(updateGroup);
        (await updateContext.SaveChangesAsync()).IsSuccess.Should().BeFalse();

        var removeContext = IntegrationTestFixture.DataContext;
        removeContext.Remove(removeGroup);
        (await removeContext.SaveChangesAsync()).IsSuccess.Should().BeFalse();

        await using var verifyDb = OpenDbContext();
        var persistedUpdate = await verifyDb.Set<RegistryConfigurationGroup>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(item => item.Id == updateGroup.Id);
        var persistedRemove = await verifyDb.Set<RegistryConfigurationGroup>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(item => item.Id == removeGroup.Id);
        persistedUpdate.Description.Should().NotBe("Unauthorized update");
        persistedRemove.IsDeleted.Should().BeFalse();
    }

    private static RegistryConfigurationGroup CreateForeignRegistryGroup(Guid tenantId, string suffix) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        Name = $"Foreign {suffix} {Guid.NewGuid():N}",
        Description = "Original",
        SystemReferenceId = Guid.NewGuid(),
        IsEnabled = true,
        CreatedAt = DateTime.UtcNow,
        ConcurrencyStamp = Guid.NewGuid()
    };

    private static async Task AssertRemoteLifecycle<TEntity>(
        TEntity entity,
        Action<TEntity> mutate,
        Action<TEntity> assertUpdated)
        where TEntity : BaseModel
    {
        var createContext = IntegrationTestFixture.DataContext;
        createContext.Add(entity);
        var create = await createContext.SaveChangesAsync();
        create.IsSuccess.Should().BeTrue(create.Message);

        await using (var createdDb = OpenDbContext())
        {
            var created = await createdDb.Set<TEntity>()
                .IgnoreQueryFilters()
                .AsNoTracking()
                .SingleAsync(item => item.Id == entity.Id);
            created.IsDeleted.Should().BeFalse();
        }

        var updateContext = IntegrationTestFixture.DataContext;
        var createdRemote = await updateContext.Query<TEntity>()
            .Where(item => item.Id == entity.Id)
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException($"Created {typeof(TEntity).Name} was not returned by the remote query.");
        mutate(createdRemote);
        createdRemote.ModifiedAt = DateTime.UtcNow;
        updateContext.Update(createdRemote);
        var update = await updateContext.SaveChangesAsync();
        update.IsSuccess.Should().BeTrue(update.Message);

        await using (var updatedDb = OpenDbContext())
        {
            var updated = await updatedDb.Set<TEntity>()
                .IgnoreQueryFilters()
                .AsNoTracking()
                .SingleAsync(item => item.Id == entity.Id);
            assertUpdated(updated);
        }

        var updatedRemote = await IntegrationTestFixture.DataContext.Query<TEntity>()
            .Where(item => item.Id == entity.Id)
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException($"Updated {typeof(TEntity).Name} was not returned by the remote query.");
        var removeContext = IntegrationTestFixture.DataContext;
        removeContext.Remove(updatedRemote);
        var remove = await removeContext.SaveChangesAsync();
        remove.IsSuccess.Should().BeTrue(remove.Message);

        await using var removedDb = OpenDbContext();
        var removed = await removedDb.Set<TEntity>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(item => item.Id == entity.Id);
        removed.IsDeleted.Should().BeTrue();
        removed.DeletedAt.Should().NotBeNull();
    }

    private static async Task AssertTenantMismatchRejected<TEntity>(TEntity entity)
        where TEntity : BaseModel
    {
        var context = IntegrationTestFixture.DataContext;
        context.Add(entity);

        var save = await context.SaveChangesAsync();

        save.IsSuccess.Should().BeFalse();
        await using var db = OpenDbContext();
        (await db.Set<TEntity>().IgnoreQueryFilters().AnyAsync(item => item.Id == entity.Id))
            .Should().BeFalse();
    }

    private static async Task AssertRelationshipRejected<TEntity>(TEntity entity)
        where TEntity : BaseModel
    {
        var context = IntegrationTestFixture.DataContext;
        context.Add(entity);

        var save = await context.SaveChangesAsync();

        save.IsSuccess.Should().BeFalse();
        await using var db = OpenDbContext();
        (await db.Set<TEntity>().IgnoreQueryFilters().AnyAsync(item => item.Id == entity.Id))
            .Should().BeFalse();
    }

    private static async Task<MutationSeed> SeedMutationDependencies()
    {
        await using var db = OpenDbContext();
        var identity = new IdentityInformation
        {
            Id = Guid.NewGuid(),
            TenantId = IntegrationTestFixture.TestTenantId,
            IdentityName = $"Remote mutation owner {Guid.NewGuid():N}",
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            ConcurrencyStamp = Guid.NewGuid()
        };
        var credential = new IdentityCredential
        {
            Id = Guid.NewGuid(),
            TenantId = IntegrationTestFixture.TestTenantId,
            IdentityInfoId = identity.Id,
            UserName = $"remote_mutation_{Guid.NewGuid():N}",
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            ConcurrencyStamp = Guid.NewGuid()
        };
        var contactGroup = new IdentityContactGroup
        {
            Id = Guid.NewGuid(),
            TenantId = IntegrationTestFixture.TestTenantId,
            Name = $"Remote group {Guid.NewGuid():N}",
            SystemReferenceId = Guid.NewGuid(),
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow
        };
        var registryGroup = new RegistryConfigurationGroup
        {
            Id = Guid.NewGuid(),
            TenantId = IntegrationTestFixture.TestTenantId,
            Name = $"Remote registry group {Guid.NewGuid():N}",
            SystemReferenceId = Guid.NewGuid(),
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow
        };

        db.AddRange(identity, credential, contactGroup, registryGroup);
        await db.SaveChangesAsync();
        return new MutationSeed(
            identity.Id,
            credential.Id,
            contactGroup.Id,
            registryGroup.Id,
            IdentityConstants.ContactType.Email,
            IdentityConstants.AddressType.Home);
    }

    private static async Task<MutationSeed> SeedForeignTenantDependencies()
    {
        var tenantId = Guid.NewGuid();
        await using var db = OpenDbContext();
        var tenant = new Tenant
        {
            Id = tenantId,
            TenantId = tenantId,
            Name = $"Foreign tenant {tenantId:N}",
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            ConcurrencyStamp = Guid.NewGuid()
        };
        var identity = new IdentityInformation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            IdentityName = "Foreign identity",
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            ConcurrencyStamp = Guid.NewGuid()
        };
        var credential = new IdentityCredential
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            IdentityInfoId = identity.Id,
            UserName = $"foreign_{Guid.NewGuid():N}",
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            ConcurrencyStamp = Guid.NewGuid()
        };
        var contactGroup = new IdentityContactGroup
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Foreign contacts",
            SystemReferenceId = Guid.NewGuid(),
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            ConcurrencyStamp = Guid.NewGuid()
        };
        var contactType = new IdentityContactType
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Foreign contact type",
            SystemReferenceId = Guid.NewGuid(),
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            ConcurrencyStamp = Guid.NewGuid()
        };
        var addressType = new IdentityAddressType
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Foreign address type",
            SystemReferenceId = Guid.NewGuid(),
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            ConcurrencyStamp = Guid.NewGuid()
        };
        var registryGroup = new RegistryConfigurationGroup
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Foreign registry",
            SystemReferenceId = Guid.NewGuid(),
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            ConcurrencyStamp = Guid.NewGuid()
        };

        db.AddRange(tenant, identity, credential, contactGroup, contactType, addressType, registryGroup);
        await db.SaveChangesAsync();
        return new MutationSeed(
            identity.Id,
            credential.Id,
            contactGroup.Id,
            registryGroup.Id,
            contactType.Id,
            addressType.Id);
    }

    private static AppDbContext OpenDbContext() =>
        new IntegrationTestBaseAccessor().OpenDbContext();

    private sealed record MutationSeed(
        Guid IdentityId,
        Guid CredentialId,
        Guid ContactGroupId,
        Guid RegistryGroupId,
        Guid ContactTypeId,
        Guid AddressTypeId);

    private sealed class IntegrationTestBaseAccessor : IntegrationTestBase
    {
        public AppDbContext OpenDbContext() => CreateDbContext();
    }
}
