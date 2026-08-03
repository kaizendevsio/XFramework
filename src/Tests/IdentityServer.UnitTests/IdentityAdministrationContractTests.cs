using System.Reflection;
using Bolt.Domain.Shared.Contracts.Requests;
using FluentAssertions;
using IdentityServer.Api.Features.Authorization.AssignCredentialRole;
using IdentityServer.Api.Features.Identities.Create;
using IdentityServer.Api.Features.Identities.SetEnabled;
using IdentityServer.Api.Features.Identities.UpdateProfile;
using IdentityServer.Domain.Shared.Contracts.Requests;
using IdentityServer.Domain.Shared.Contracts.Responses;
using NUnit.Framework;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Integration.Attributes;

namespace IdentityServer.UnitTests;

[TestFixture]
[Category("Module:IdentityServer")]
[Category("Area:Contract")]
public sealed class IdentityAdministrationContractTests
{
    [Test]
    public void AdministrationRequests_UseStableScalarResponseContracts()
    {
        AssertBoltResponse<CreateIdentityRequest, CmdResponse<IdentityAdministrationResponse>>();
        AssertBoltResponse<UpdateIdentityProfileRequest, CmdResponse<IdentityAdministrationResponse>>();
        AssertBoltResponse<SetIdentityEnabledRequest, CmdResponse<IdentityAdministrationResponse>>();
        AssertBoltResponse<AssignCredentialRoleRequest, QueryResponse<AssignedCredentialRoleResponse>>();

        AssertScalarContract<IdentityAdministrationResponse>();
        AssertScalarContract<AssignedCredentialRoleResponse>();
    }

    [TestCase(typeof(CreateIdentityEndpoint))]
    [TestCase(typeof(UpdateIdentityProfileEndpoint))]
    [TestCase(typeof(SetIdentityEnabledEndpoint))]
    [TestCase(typeof(AssignCredentialRoleEndpoint))]
    public void AdministrationHttpEndpoints_AreAuthenticatedAndIncludedInOpenApi(Type endpointType)
    {
        var endpoint = endpointType.GetMethod("HandleHttp", BindingFlags.Public | BindingFlags.Static);

        endpoint.Should().NotBeNull();
        var mapPost = endpoint!.GetCustomAttribute<MapPostAttribute>();
        mapPost.Should().NotBeNull();
        mapPost!.RequireAuthorization.Should().BeTrue();
        mapPost.ExcludeFromOpenApi.Should().BeFalse();
    }

    private static void AssertBoltResponse<TRequest, TResponse>()
    {
        typeof(TRequest).GetInterfaces().Should().Contain(
            typeof(IBoltRequest<TRequest, TResponse>));
    }

    private static void AssertScalarContract<TResponse>()
    {
        var unsupportedProperties = typeof(TResponse)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => !IsScalar(property.PropertyType))
            .Select(property => property.Name)
            .ToList();

        unsupportedProperties.Should().BeEmpty(
            $"{typeof(TResponse).Name} must not expose EF entities or navigation collections");
    }

    private static bool IsScalar(Type type)
    {
        var scalarType = Nullable.GetUnderlyingType(type) ?? type;
        return scalarType.IsEnum
               || scalarType == typeof(string)
               || scalarType == typeof(Guid)
               || scalarType == typeof(DateOnly)
               || scalarType == typeof(DateTime)
               || scalarType == typeof(bool);
    }
}
