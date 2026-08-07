using FluentAssertions;
using NUnit.Framework;
using XFramework.Core.DataContext;
using XFramework.Domain.Shared.Attributes;

namespace GeneratedAuthorizationContractTests.IdentityServer;

[TestFixture]
[Category("Kind:Integration")]
[Category("Module:IdentityServer")]
[Category("Area:GeneratedAuthorization")]
public sealed class GeneratedEntityAuthorizationCompletenessTests
{
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
                policy.AllowRemoteQuery.Should().BeTrue();
                policy.AllowRemoteMutation.Should().Be(
                    allowsRemoteMutation && operation != GeneratedEntityOperation.Read);
            }
        }
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
