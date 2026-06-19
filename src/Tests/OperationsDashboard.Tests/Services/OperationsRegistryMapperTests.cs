using Bolt.Domain.Shared.Contracts.ServiceDiscovery;
using FluentAssertions;
using NUnit.Framework;
using XFramework.Operations.Dashboard.Services;

namespace OperationsDashboard.Tests.Services;

[TestFixture]
public sealed class OperationsRegistryMapperTests
{
    [Test]
    public void CreateSnapshot_ServicesAndModules_ComputesSummaryAndGroupsModules()
    {
        var services = new[]
        {
            new BoltServiceRegistryItem
            {
                ClientId = "client-identity",
                ClientName = "IdentityServer",
                ServiceName = "IdentityServer",
                DisplayName = "Identity Server",
                Status = BoltRegistryStatus.Online,
                ConnectionCount = 2,
                LastSeenAt = DateTime.UtcNow
            },
            new BoltServiceRegistryItem
            {
                ClientId = "client-wallets",
                ClientName = "Wallets",
                ServiceName = "Wallets",
                DisplayName = "Wallets",
                Status = BoltRegistryStatus.Offline,
                ConnectionCount = 0,
                LastSeenAt = DateTime.UtcNow.AddMinutes(-5)
            }
        };

        var modules = new[]
        {
            new BoltModuleRegistryItem
            {
                ClientId = "client-identity",
                ModuleKey = "identity",
                DisplayName = "Identity",
                Status = BoltRegistryStatus.Online,
                Features =
                [
                    new BoltTenantModuleFeatureRegistryItem { Key = "identity.users" }
                ]
            }
        };

        var snapshot = OperationsRegistryMapper.CreateSnapshot(services, modules, DateTimeOffset.UtcNow);

        snapshot.Summary.TotalServices.Should().Be(2);
        snapshot.Summary.OnlineServices.Should().Be(1);
        snapshot.Summary.OfflineServices.Should().Be(1);
        snapshot.Summary.ActiveInstances.Should().Be(2);
        snapshot.Services.Single(x => x.ClientId == "client-identity").Modules.Should().ContainSingle()
            .Which.FeatureCount.Should().Be(1);
    }

    [Test]
    public void CreateSnapshot_RequiredDependencyMissing_MarksServiceDegraded()
    {
        var service = new BoltServiceRegistryItem
        {
            ClientId = "client-a",
            ClientName = "Messaging",
            ServiceName = "Messaging",
            Status = BoltRegistryStatus.Online,
            ConnectionCount = 1,
            LastSeenAt = DateTime.UtcNow,
            DependencyStatuses =
            [
                new BoltDependencyStatus
                {
                    Requirement = new BoltDependencyRequirement
                    {
                        Kind = BoltDependencyKind.Service,
                        Key = "IdentityServer",
                        DisplayName = "Identity Server",
                        Required = true
                    },
                    IsSatisfied = false,
                    Message = "Identity Server is missing."
                }
            ]
        };

        var snapshot = OperationsRegistryMapper.CreateSnapshot([service], [], DateTimeOffset.UtcNow);

        snapshot.Summary.DegradedServices.Should().Be(1);
        snapshot.Services.Single().Status.Should().Be("Degraded");
        snapshot.Services.Single().MissingRequiredDependencies.Should().Be(1);
    }

    [Test]
    public void CreateSnapshot_BuiltInService_MapsTraceServiceNameToOpenTelemetryName()
    {
        var service = new BoltServiceRegistryItem
        {
            ClientId = "client-identity",
            ClientName = "IdentityServer",
            ServiceName = "IdentityServer",
            Status = BoltRegistryStatus.Online,
            ConnectionCount = 1,
            LastSeenAt = DateTime.UtcNow
        };

        var snapshot = OperationsRegistryMapper.CreateSnapshot([service], [], DateTimeOffset.UtcNow);

        snapshot.Services.Single().TraceServiceName.Should().Be("XFramework.IdentityServer.Api");
    }

    [Test]
    public void CreateSnapshot_MetadataTraceServiceName_PreservesExternalModuleName()
    {
        var service = new BoltServiceRegistryItem
        {
            ClientId = "client-barangay",
            ClientName = "JuanBarangay",
            ServiceName = "JuanBarangay",
            Status = BoltRegistryStatus.Online,
            ConnectionCount = 1,
            LastSeenAt = DateTime.UtcNow,
            Manifest =
            {
                Metadata =
                {
                    ["TraceServiceName"] = "JuanBarangay.Api"
                }
            }
        };

        var snapshot = OperationsRegistryMapper.CreateSnapshot([service], [], DateTimeOffset.UtcNow);

        snapshot.Services.Single().TraceServiceName.Should().Be("JuanBarangay.Api");
    }
}
