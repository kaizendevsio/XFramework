using FluentAssertions;
using NUnit.Framework;
using XFramework.Core.DataContext;
using XFramework.Domain.Shared.Attributes;
using XFramework.Integration.Security;

namespace GeneratedAuthorizationContractTests.IdentityServer;

[TestFixture]
[Category("Kind:Integration")]
[Category("Module:IdentityServer")]
[Category("Area:GeneratedAuthorization")]
public sealed class GeneratedEntityAuthorizationCompletenessTests
{
    [Test]
    public void OpaqueCredentials_AreNotExposedThroughGeneratedRemoteQueriesOrCrud()
    {
        var registry = typeof(global::IdentityServer.Api.Features.Auth.Opaque.OpaqueAuthEndpoint)
            .Assembly.GetType("XFramework.Core.DataContext.DataContextEntityRegistrations", true)!;
        var entities = (Dictionary<string, Type>)registry.GetMethod("GetDataContextEntityTypes")!.Invoke(null, null)!;
        entities.Values.Should().NotContain(typeof(global::IdentityServer.Domain.Shared.Contracts.OpaqueCredential));
    }

    [TestCase("IdentityRoleType", "identity.roles")]
    [TestCase("IdentityRoleTypeGroup", "identity.roles")]
    [TestCase("IdentityContactType", "identity.contacts")]
    [TestCase("IdentityContactGroup", "identity.contacts")]
    [TestCase("IdentityAddressType", "identity.addresses")]
    [TestCase("IdentityVerificationType", "identity.verifications")]
    [TestCase("SessionType", "identity.sessions")]
    public void ReferenceData_AllOperationsRequireCapabilitiesAndManagedTenantDelegation(string entityName, string feature)
    {
        var registryType = typeof(global::IdentityServer.Api.Features.Auth.ValidateSession.ValidateIdentitySessionEndpoint)
            .Assembly.GetType("XFramework.Core.DataContext.DataContextEntityRegistrations", throwOnError: true)!;
        var policies = (IReadOnlyCollection<GeneratedEntityAuthorizationPolicy>)registryType
            .GetMethod("GetDataContextAuthorizationPolicies")!.Invoke(null, null)!;
        var entityPolicies = policies.Where(policy => policy.EntityTypeName == entityName).ToArray();
        entityPolicies.Should().HaveCount(4);
        foreach (var policy in entityPolicies)
        {
            policy.AllowRemoteMutation.Should().Be(policy.Operation != GeneratedEntityOperation.Read);
            var action = policy.Operation == GeneratedEntityOperation.Read ? "view" : policy.Operation.ToString().ToLowerInvariant();
            policy.RequiredCapability.Should().Be($"{feature}:{action}");
            policy.TenantAccessMode.Should().Be(XFramework.Integration.Security.TenantAccessMode.DelegatedTenant);
            policy.RequiredCrossTenantActorCapabilities.Should().ContainSingle().Which.Should().Be("identity.tenants:manage");
            policy.AllowServiceOnly.Should().BeFalse();
        }
    }

    [Test]
    public void GeneratedIdentityEntities_HaveCompleteServerOwnedAuthorizationPolicies()
    {
        var registryType = typeof(global::IdentityServer.Api.Features.Auth.ValidateSession.ValidateIdentitySessionEndpoint)
            .Assembly
            .GetType("XFramework.Core.DataContext.DataContextEntityRegistrations", throwOnError: true)!;
        var entities = (Dictionary<string, Type>)registryType
            .GetMethod("GetDataContextEntityTypes")!
            .Invoke(null, null)!;
        var policies = ((IReadOnlyCollection<GeneratedEntityAuthorizationPolicy>)registryType
                .GetMethod("GetDataContextAuthorizationPolicies")!
                .Invoke(null, null)!)
            .ToDictionary(policy => (policy.EntityTypeName, policy.Operation));

        foreach (var (entityName, entityType) in entities)
        {
            var attribute = entityType.GetCustomAttributes(typeof(GenerateEndpointsAttribute), false)
                .Cast<GenerateEndpointsAttribute>()
                .Single();
            var allowsRemoteMutation = Attribute.IsDefined(
                entityType,
                typeof(AllowRemoteDataContextMutationAttribute));
            foreach (var operation in ExpectedOperations(attribute.Actions, allowsRemoteMutation))
            {
                policies.Should().ContainKey((entityName, operation));
                var policy = policies[(entityName, operation)];
                policy.ActorRequirement.Should().Be(XFramework.Integration.Security.ActorRequirement.Required);
                policy.AuthorizationFeature.Should().StartWith("identity");
                policy.RequiredCapability.Should().StartWith($"{policy.AuthorizationFeature}:");
                policy.AllowServiceOnly.Should().BeFalse();
                policy.AllowRemoteQuery.Should().Be(operation == GeneratedEntityOperation.Read);
                policy.AllowRemoteMutation.Should().Be(
                    allowsRemoteMutation && operation != GeneratedEntityOperation.Read);
            }
        }
    }

    [TestCase(typeof(global::IdentityServer.Domain.Shared.Contracts.TenantModuleFeature))]
    [TestCase(typeof(global::IdentityServer.Domain.Shared.Contracts.IdentityInformation))]
    [TestCase(typeof(global::IdentityServer.Domain.Shared.Contracts.IdentityCredential))]
    [TestCase(typeof(global::IdentityServer.Domain.Shared.Contracts.Tenant))]
    [TestCase(typeof(global::IdentityServer.Domain.Shared.Contracts.Session))]
    [TestCase(typeof(global::IdentityServer.Domain.Shared.Contracts.AuthorizationLog))]
    public void ManagedTenantPortalEntity_ReadPolicy_AllowsDelegation(Type entityType)
    {
        var registryType = typeof(global::IdentityServer.Api.Features.Auth.ValidateSession.ValidateIdentitySessionEndpoint)
            .Assembly
            .GetType("XFramework.Core.DataContext.DataContextEntityRegistrations", throwOnError: true)!;
        var entities = (Dictionary<string, Type>)registryType
            .GetMethod("GetDataContextEntityTypes")!
            .Invoke(null, null)!;
        var policies = (IReadOnlyCollection<GeneratedEntityAuthorizationPolicy>)registryType
            .GetMethod("GetDataContextAuthorizationPolicies")!
            .Invoke(null, null)!;
        var entityName = entities.Single(candidate => candidate.Value == entityType).Key;

        var policy = policies.Single(candidate =>
            candidate.EntityTypeName == entityName &&
            candidate.Operation == GeneratedEntityOperation.Read);

        policy.TenantAccessMode.Should().Be(XFramework.Integration.Security.TenantAccessMode.DelegatedTenant);
        policy.RequiredCrossTenantActorCapabilities.Should().ContainSingle()
            .Which.Should().Be("identity.tenants:manage");
    }

    [TestCase("Tenant")]
    [TestCase("Session")]
    [TestCase("AuthorizationLog")]
    public void ManagedTenantPortalEntity_EvaluatedRead_RequiresCapabilityAndDelegation(string entityName)
    {
        var registryType = typeof(global::IdentityServer.Api.Features.Auth.ValidateSession.ValidateIdentitySessionEndpoint)
            .Assembly.GetType("XFramework.Core.DataContext.DataContextEntityRegistrations", true)!;
        var policies = (IReadOnlyCollection<GeneratedEntityAuthorizationPolicy>)registryType
            .GetMethod("GetDataContextAuthorizationPolicies")!.Invoke(null, null)!;
        var policy = policies.Single(candidate => candidate.EntityTypeName == entityName &&
            candidate.Operation == GeneratedEntityOperation.Read);
        var homeTenantId = Guid.NewGuid();
        var targetTenantId = Guid.NewGuid();

        TrustedInvocationContext Context(Guid tenantId, params string[] capabilities) => new(
            new TrustedActorIdentity(Guid.NewGuid(), Guid.NewGuid(), homeTenantId, Guid.NewGuid(),
                new HashSet<string>(), capabilities.ToHashSet(), "test", DateTimeOffset.UtcNow.AddMinutes(5)),
            null, tenantId, tenantId, Guid.NewGuid());

        policy.AllowRemoteMutation.Should().BeFalse();
        GeneratedEntityAuthorizationEvaluator.Evaluate(
            Context(targetTenantId, policy.RequiredCapability!, "identity.tenants:manage"), policy)
            .IsSuccess.Should().BeTrue();
        GeneratedEntityAuthorizationEvaluator.Evaluate(
            Context(targetTenantId, policy.RequiredCapability!), policy).StatusCode.Should().Be(403);
        GeneratedEntityAuthorizationEvaluator.Evaluate(
            Context(targetTenantId, "identity.tenants:manage"), policy).StatusCode.Should().Be(403);
        GeneratedEntityAuthorizationEvaluator.Evaluate(
            Context(homeTenantId, policy.RequiredCapability!), policy).IsSuccess.Should().BeTrue();
        GeneratedEntityAuthorizationEvaluator.Evaluate(null, policy).IsSuccess.Should().BeFalse();
    }

    private static IEnumerable<GeneratedEntityOperation> ExpectedOperations(
        EndpointActions actions,
        bool allowsRemoteMutation)
    {
        if ((actions & EndpointActions.ReadOnly) != 0)
            yield return GeneratedEntityOperation.Read;
        if (allowsRemoteMutation || (actions & EndpointActions.Create) != 0)
            yield return GeneratedEntityOperation.Create;
        if (allowsRemoteMutation || (actions & EndpointActions.Update) != 0)
            yield return GeneratedEntityOperation.Update;
        if (allowsRemoteMutation || (actions & EndpointActions.Delete) != 0)
            yield return GeneratedEntityOperation.Delete;
    }
}
