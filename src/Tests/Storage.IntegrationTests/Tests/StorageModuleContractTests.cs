using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using NUnit.Framework;
using Storage.Api.Health;
using Storage.Api.Services;
using Storage.IntegrationTests.Infrastructure;
using XFramework.Domain.Migrations;
using XFramework.Domain.Shared.Contracts;
using XFramework.Integration.Attributes;
using XFramework.Integration.Security;
using XFramework.TestInfrastructure;

namespace StorageModuleContractTests;

[TestFixture]
[Category(TestCategories.Storage)]
public sealed class StorageModuleContractTests
{
    [Test]
    public void GeneratedRestEndpoints_RequireActorCapabilitiesWithoutServiceIdentity()
    {
        var endpoints = typeof(StorageService).Assembly.GetTypes()
            .Where(type => type.Namespace?.StartsWith("Storage.Api.Features", StringComparison.Ordinal) == true)
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Static))
            .Select(method => method.GetCustomAttribute<MapEndpointAttribute>())
            .Where(attribute => attribute is not null)
            .Cast<MapEndpointAttribute>()
            .ToList();

        endpoints.Should().HaveCount(14);
        foreach (var endpoint in endpoints)
        {
            endpoint.RequireAuthorization.Should().BeTrue();
            endpoint.RequiredServiceScopes.Should().NotBeNull().And.BeEmpty();
            endpoint.ActorRequirement.Should().Be(ActorRequirement.Required);
            endpoint.TenantAccessMode.Should().Be(TenantAccessMode.ActorTenant);
            endpoint.RequiredActorCapabilities.Should().ContainSingle();
            endpoint.Capability.Should().BeOneOf(
                StorageAuthorizationCapabilities.ViewKey,
                StorageAuthorizationCapabilities.ManageKey);

            var expectedCapability = endpoint.Capability == StorageAuthorizationCapabilities.ViewKey
                ? StorageAuthorizationCapabilities.View
                : StorageAuthorizationCapabilities.Manage;
            endpoint.RequiredActorCapabilities.Should().Equal(expectedCapability);
        }
    }

    [Test]
    public void ProductionHardeningMigration_PreservesExistingRowsAndFailsLegacyPublicUrlsClosed()
    {
        var operations = new StorageProductionHardening().UpOperations;

        var partStatus = operations.OfType<AddColumnOperation>()
            .Single(operation => operation.Schema == "Storage" &&
                                 operation.Table == "StorageUploadPart" &&
                                 operation.Name == "Status");
        partStatus.DefaultValue.Should().Be((int)StorageUploadPartStatus.Uploaded);

        var bucketPurpose = operations.OfType<AddColumnOperation>()
            .Single(operation => operation.Schema == "Storage" &&
                                 operation.Table == "StorageTenantBucket" &&
                                 operation.Name == "Purpose");
        bucketPurpose.DefaultValue.Should().Be((int)StorageBucketPurpose.Private);

        operations.OfType<CreateIndexOperation>()
            .Single(operation => operation.Schema == "Storage" &&
                                 operation.Table == "StorageTenantBucket" &&
                                 operation.Name == "ix_storagetenantbucket_tenant_provider")
            .Columns.Should().Equal("TenantId", "ProviderProfileId", "Purpose");

        operations.OfType<SqlOperation>()
            .Should().ContainSingle(operation =>
                operation.Sql.Contains("SET \"PublicUrl\" = NULL, \"CdnBaseUrl\" = NULL", StringComparison.Ordinal) &&
                operation.Sql.Contains("WHERE \"Visibility\" = 1", StringComparison.Ordinal));
    }

    [Test]
    public async Task ProviderReadinessHealthCheck_ReportsProviderFailure()
    {
        var provider = new IntegrationStorageObjectProvider();
        var check = new StorageProviderReadinessHealthCheck(
            new IntegrationStorageProviderFactory(provider),
            Options.Create(new StorageOptions { ReadinessTimeoutSeconds = 1 }));
        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration(
                "storage-provider",
                check,
                HealthStatus.Unhealthy,
                ["ready"])
        };

        (await check.CheckHealthAsync(context)).Status.Should().Be(HealthStatus.Healthy);

        provider.FailReadiness = true;
        (await check.CheckHealthAsync(context)).Status.Should().Be(HealthStatus.Unhealthy);
    }
}
