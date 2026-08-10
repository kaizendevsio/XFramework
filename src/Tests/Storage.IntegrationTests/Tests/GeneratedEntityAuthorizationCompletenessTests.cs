using FluentAssertions;
using NUnit.Framework;
using Storage.Api.Services;
using Storage.Domain.Shared.Contracts;
using XFramework.Core.DataContext;
using XFramework.Domain.Shared.Attributes;
using XFramework.Domain.Shared.Contracts;
using XFramework.Integration.Security;

namespace GeneratedAuthorizationContractTests.Storage;

[TestFixture]
[Category("Kind:Integration")]
[Category("Module:Storage")]
[Category("Area:GeneratedAuthorization")]
public sealed class GeneratedEntityAuthorizationCompletenessTests
{
    [Test]
    public void GeneratedStorageEntities_HaveCompleteCanonicalAuthorizationPolicies()
    {
        var registryType = typeof(StorageService).Assembly
            .GetType("XFramework.Core.DataContext.DataContextEntityRegistrations", throwOnError: true)!;
        var entities = (Dictionary<string, Type>)registryType
            .GetMethod("GetDataContextEntityTypes")!
            .Invoke(null, null)!;
        var policies = ((IReadOnlyCollection<GeneratedEntityAuthorizationPolicy>)registryType
                .GetMethod("GetDataContextAuthorizationPolicies")!
                .Invoke(null, null)!)
            .ToDictionary(policy => (policy.EntityTypeName, policy.Operation));
        var expectedEntities = new[] { nameof(StorageFile), nameof(StorageFileType) };

        entities.Keys.Should().BeEquivalentTo(expectedEntities);
        foreach (var (entityName, entityType) in entities)
        {
            var attribute = entityType.GetCustomAttributes(typeof(GenerateEndpointsAttribute), false)
                .Cast<GenerateEndpointsAttribute>()
                .Single();
            attribute.AuthorizationFeature.Should().Be(StorageAuthorizationCapabilities.Feature);
            attribute.ReadCapability.Should().Be(StorageAuthorizationCapabilities.ViewKey);
            attribute.Actions.Should().Be(EndpointActions.ReadOnly);
            attribute.Type.Should().Be(EndpointType.Both);
            attribute.CacheDurationSeconds.Should().Be(0);

            policies.Should().ContainKey((entityName, GeneratedEntityOperation.Read));
            var policy = policies[(entityName, GeneratedEntityOperation.Read)];
            policy.ActorRequirement.Should().Be(ActorRequirement.Required);
            policy.TenantAccessMode.Should().Be(TenantAccessMode.ActorTenant);
            policy.AuthorizationFeature.Should().Be(StorageAuthorizationCapabilities.Feature);
            policy.RequiredCapability.Should().Be(StorageAuthorizationCapabilities.View);
            policy.AllowRemoteQuery.Should().BeTrue();
            policy.AllowRemoteMutation.Should().BeFalse();
            policy.AllowServiceOnly.Should().BeFalse();
            policy.RequiredServiceScopes.Should().BeEmpty();
        }
    }
}
