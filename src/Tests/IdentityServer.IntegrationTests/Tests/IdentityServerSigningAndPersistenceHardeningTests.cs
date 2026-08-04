using IdentityServer.Api.Services;
using IdentityServer.Domain.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Base;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.TestInfrastructure;

namespace IdentityServer.IntegrationTests.Tests;

[TestFixture]
[NonParallelizable]
[Category(TestCategories.Integration)]
[Category(TestCategories.ExtendedIntegration)]
[Category(TestCategories.IdentityServer)]
public sealed class IdentityServerSigningAndPersistenceHardeningTests : IntegrationTestBase
{
    private const int MaxPublishedSigningKeys = 32;

    [Test]
    public void AuthorizationMutationRows_UseConcurrencyTokensAndExpectedIndexes()
    {
        using var db = CreateDbContext();

        Type[] authorizationMutationTypes =
        [
            typeof(IdentityCredential),
            typeof(IdentityVerification),
            typeof(Session),
            typeof(IdentityRole),
            typeof(IdentityRoleType),
            typeof(TenantModuleFeature),
            typeof(TenantAuthorizationPolicy),
            typeof(IdentityRoleTypeFeaturePermission),
            typeof(IdentityRoleFeaturePermissionOverride)
        ];

        foreach (var type in authorizationMutationTypes)
        {
            var concurrencyProperty = db.Model.FindEntityType(type)!
                .FindProperty(nameof(BaseModel.ConcurrencyStamp));

            concurrencyProperty.Should().NotBeNull($"{type.Name} is an authorization mutation row");
            concurrencyProperty!.IsConcurrencyToken.Should().BeTrue();
        }

        string[] roleAssignmentIndexProperties =
            [nameof(BaseModel.TenantId), nameof(IdentityRole.CredentialId), nameof(IdentityRole.TypeId)];
        var roleIndexes = db.Model.FindEntityType(typeof(IdentityRole))!.GetIndexes();
        roleIndexes.Should().Contain(index =>
            index.IsUnique &&
            index.Properties.Select(property => property.Name)
                .SequenceEqual(roleAssignmentIndexProperties));

        string[] contactLookupIndexProperties =
            [nameof(BaseModel.TenantId), nameof(IdentityContact.Value), nameof(IdentityContact.TypeId)];
        var contactIndexes = db.Model.FindEntityType(typeof(IdentityContact))!.GetIndexes();
        contactIndexes.Should().Contain(index =>
            index.Properties.Select(property => property.Name)
                .SequenceEqual(contactLookupIndexProperties));
    }

    [Test]
    public async Task CredentialConcurrentWrites_RejectTheStaleWriter()
    {
        var identityId = Guid.NewGuid();
        Guid credentialId;
        await using (var seedDb = CreateDbContext())
        {
            seedDb.Add(new IdentityInformation
            {
                Id = identityId,
                TenantId = IntegrationTestFixture.TestTenantId,
                FirstName = "Concurrency",
                LastName = "Test",
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                ConcurrencyStamp = Guid.NewGuid()
            });
            var credential = new IdentityCredential
            {
                Id = Guid.NewGuid(),
                TenantId = IntegrationTestFixture.TestTenantId,
                IdentityInfoId = identityId,
                UserName = $"concurrency-{Guid.NewGuid():N}",
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                ConcurrencyStamp = Guid.NewGuid()
            };
            seedDb.Add(credential);
            await seedDb.SaveChangesAsync();
            credentialId = credential.Id;
        }

        try
        {
            await using var firstDb = CreateDbContext();
            await using var staleDb = CreateDbContext();
            var firstCopy = await firstDb.Set<IdentityCredential>().SingleAsync(x => x.Id == credentialId);
            var staleCopy = await staleDb.Set<IdentityCredential>().SingleAsync(x => x.Id == credentialId);

            firstCopy.UserAlias = "first-writer";
            firstCopy.ConcurrencyStamp = Guid.NewGuid();
            await firstDb.SaveChangesAsync();

            staleCopy.UserAlias = "stale-writer";
            staleCopy.ConcurrencyStamp = Guid.NewGuid();
            var saveStale = async () => await staleDb.SaveChangesAsync();

            await saveStale.Should().ThrowAsync<DbUpdateConcurrencyException>();
        }
        finally
        {
            await using var cleanupDb = CreateDbContext();
            await cleanupDb.Set<IdentityCredential>()
                .Where(x => x.Id == credentialId)
                .ExecuteDeleteAsync();
            await cleanupDb.Set<IdentityInformation>()
                .Where(x => x.Id == identityId)
                .ExecuteDeleteAsync();
        }
    }

    [Test]
    public async Task RotateSigningKey_RetainsPreviousKeyForConfiguredAccessTokenLifetime()
    {
        await using var scope = IntegrationTestFixture.Services.CreateAsyncScope();
        {
            IntegrationTestFixture.EstablishTrustedActorContext(
                scope.ServiceProvider,
                IntegrationTestFixture.TestTenantId,
                IntegrationTestFixture.TestCredentialId);
            var service = scope.ServiceProvider.GetRequiredService<IServiceIdentityService>();
            var configuration = scope.ServiceProvider.GetRequiredService<ServiceIdentityConfiguration>();
            var current = await service.GetSigningKeysAsync(new GetServiceSigningKeysRequest());
            var activeKey = current.Data!.Keys.Single(key => key.IsActive);
            var startedAt = DateTime.UtcNow;

            var rotation = await service.RotateSigningKeyAsync(new RotateServiceSigningKeyRequest
            {
                Reason = "integration-test",
                Metadata = new RequestMetadata()
            });
            var completedAt = DateTime.UtcNow;

            rotation.IsSuccess.Should().BeTrue(rotation.Message);
            await using var db = CreateDbContext();
            var retiredKey = await db.Set<ServiceSigningKey>()
                .SingleAsync(key => key.KeyId == activeKey.KeyId);

            retiredKey.IsActive.Should().BeFalse();
            retiredKey.RetiredAtUtc.Should().NotBeNull();
            retiredKey.RetiredAtUtc!.Value.Should().BeOnOrAfter(
                startedAt.AddMinutes(configuration.TokenLifetimeMinutes));
            retiredKey.RetiredAtUtc.Value.Should().BeOnOrBefore(
                completedAt.AddMinutes(configuration.TokenLifetimeMinutes));
        }
    }

    [Test]
    public async Task GetSigningKeys_ConcurrentEmptyStoreBootstrap_CreatesExactlyOneActiveKey()
    {
        List<ServiceSigningKey> existingKeys;
        await using (var snapshotDb = CreateDbContext())
        {
            existingKeys = await snapshotDb.Set<ServiceSigningKey>()
                .AsNoTracking()
                .ToListAsync();
            await snapshotDb.Set<ServiceSigningKey>().ExecuteDeleteAsync();
        }

        try
        {
            var results = await Task.WhenAll(Enumerable.Range(0, 12).Select(async _ =>
            {
                await using var scope = IntegrationTestFixture.Services.CreateAsyncScope();
                IntegrationTestFixture.EstablishTrustedServiceTargetContext(
                    scope.ServiceProvider,
                    IntegrationTestFixture.TestTenantId);
                var service = scope.ServiceProvider.GetRequiredService<IServiceIdentityService>();
                return await service.GetSigningKeysAsync(new GetServiceSigningKeysRequest());
            }));

            results.Should().OnlyContain(result => result.IsSuccess);
            results
                .Select(result => result.Data!.Keys.Single(key => key.IsActive).KeyId)
                .Distinct()
                .Should().ContainSingle();

            await using var verificationDb = CreateDbContext();
            var createdKeys = await verificationDb.Set<ServiceSigningKey>()
                .AsNoTracking()
                .ToListAsync();
            createdKeys.Should().ContainSingle();
            createdKeys.Single().IsActive.Should().BeTrue();
            createdKeys.Single().CreatedBy.Should().Be("auto-bootstrap");
        }
        finally
        {
            await using var cleanupDb = CreateDbContext();
            var generatedKeys = await cleanupDb.Set<ServiceSigningKey>()
                .AsNoTracking()
                .ToListAsync();
            await cleanupDb.Set<ServiceSigningKey>().ExecuteDeleteAsync();

            foreach (var generatedKey in generatedKeys.Where(key =>
                         existingKeys.All(existing => existing.Id != key.Id)))
            {
                var path = Path.Combine(GetServiceSigningKeyDirectory(), generatedKey.PrivateKeyFileName);
                if (File.Exists(path))
                    File.Delete(path);
            }

            if (existingKeys.Count > 0)
            {
                cleanupDb.AddRange(existingKeys);
                await cleanupDb.SaveChangesAsync();
            }
        }
    }

    [Test]
    public async Task GetSigningKeys_CleansExpiredRecordsAndPrivateFiles()
    {
        var keyId = $"expired-{Guid.NewGuid():N}";
        var fileName = $"{keyId}.pem";
        var filePath = Path.Combine(GetServiceSigningKeyDirectory(), fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        await File.WriteAllTextAsync(filePath, "expired-private-key");

        await using (var db = CreateDbContext())
        {
            db.Add(new ServiceSigningKey
            {
                Id = Guid.NewGuid(),
                KeyId = keyId,
                Algorithm = "RS256",
                PrivateKeyFileName = fileName,
                PublicKeyPem = "expired-public-key",
                CreatedAtUtc = DateTime.UtcNow.AddHours(-1),
                ActivatedAtUtc = DateTime.UtcNow.AddHours(-1),
                RetiredAtUtc = DateTime.UtcNow.AddMinutes(-1),
                IsActive = false,
                CreatedBy = "integration-test"
            });
            await db.SaveChangesAsync();
        }

        try
        {
            await using var scope = IntegrationTestFixture.Services.CreateAsyncScope();
            IntegrationTestFixture.EstablishTrustedServiceTargetContext(
                scope.ServiceProvider,
                IntegrationTestFixture.TestTenantId);
            var service = scope.ServiceProvider.GetRequiredService<IServiceIdentityService>();
            var result = await service.GetSigningKeysAsync(new GetServiceSigningKeysRequest());

            result.IsSuccess.Should().BeTrue(result.Message);
            result.Data!.Keys.Should().NotContain(key => key.KeyId == keyId);
            File.Exists(filePath).Should().BeFalse();
            await using var verificationDb = CreateDbContext();
            (await verificationDb.Set<ServiceSigningKey>().AnyAsync(key => key.KeyId == keyId)).Should().BeFalse();
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
            await using var cleanupDb = CreateDbContext();
            await cleanupDb.Set<ServiceSigningKey>()
                .Where(key => key.KeyId == keyId)
                .ExecuteDeleteAsync();
        }
    }

    [Test]
    public async Task GetSigningKeys_BoundsPublishedKeyDiscovery()
    {
        var now = DateTime.UtcNow;
        var keyIds = Enumerable.Range(0, MaxPublishedSigningKeys + 8)
            .Select(_ => $"bounded-{Guid.NewGuid():N}")
            .ToArray();

        await using (var db = CreateDbContext())
        {
            db.AddRange(keyIds.Select(keyId => new ServiceSigningKey
            {
                Id = Guid.NewGuid(),
                KeyId = keyId,
                Algorithm = "RS256",
                PrivateKeyFileName = $"{keyId}.pem",
                PublicKeyPem = "bounded-public-key",
                CreatedAtUtc = now,
                ActivatedAtUtc = now,
                IsActive = false,
                CreatedBy = "integration-test"
            }));
            await db.SaveChangesAsync();
        }

        try
        {
            await using var scope = IntegrationTestFixture.Services.CreateAsyncScope();
            IntegrationTestFixture.EstablishTrustedServiceTargetContext(
                scope.ServiceProvider,
                IntegrationTestFixture.TestTenantId);
            var service = scope.ServiceProvider.GetRequiredService<IServiceIdentityService>();
            var result = await service.GetSigningKeysAsync(new GetServiceSigningKeysRequest());

            result.IsSuccess.Should().BeTrue(result.Message);
            result.Data!.Keys.Should().HaveCountLessThanOrEqualTo(MaxPublishedSigningKeys);
        }
        finally
        {
            await using var cleanupDb = CreateDbContext();
            await cleanupDb.Set<ServiceSigningKey>()
                .Where(key => keyIds.Contains(key.KeyId))
                .ExecuteDeleteAsync();
        }
    }

    private static string GetServiceSigningKeyDirectory()
    {
        var configuration = IntegrationTestFixture.Services.GetRequiredService<IConfiguration>();
        var transportSigningKeyPath = configuration[
            "ServiceIdentity:BoltTransportTokenIssuer:SigningKeyPath"]!;
        return Path.Combine(
            Path.GetDirectoryName(Path.GetFullPath(transportSigningKeyPath))!,
            "service-token-signing-keys");
    }
}
