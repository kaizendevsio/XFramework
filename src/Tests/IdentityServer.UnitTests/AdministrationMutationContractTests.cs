using System.Reflection;
using Bolt.Domain.Shared.Contracts.Requests;
using FluentAssertions;
using IdentityServer.Api.Features.Credentials.Create;
using IdentityServer.Api.Features.Credentials.Update;
using IdentityServer.Api.Features.Tenants.Create;
using IdentityServer.Api.Features.Tenants.Update;
using IdentityServer.Api.Features.Verification.Create;
using IdentityServer.Domain.Shared.Contracts.Requests;
using IdentityServer.Domain.Shared.Contracts.Responses;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using NUnit.Framework;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Integration.Attributes;

namespace IdentityServer.UnitTests;

[TestFixture]
[Category("Module:IdentityServer")]
[Category("Area:Contract")]
public sealed class AdministrationMutationContractTests
{
    [Test]
    public void WrapperMutations_UseStableScalarResponses()
    {
        AssertBoltResponse<CreateCredentialRequest, CmdResponse<CredentialAdministrationResponse>>();
        AssertBoltResponse<CreateTenantRequest, CmdResponse<TenantAdministrationResponse>>();
        AssertBoltResponse<UpdateTenantRequest, CmdResponse<TenantAdministrationResponse>>();

        AssertScalarContract<CredentialAdministrationResponse>();
        AssertScalarContract<VerificationAdministrationResponse>();
        AssertScalarContract<TenantAdministrationResponse>();
    }

    [Test]
    public void CredentialUpdate_ExposesOnlyDedicatedNullableAdministrationFields()
    {
        typeof(UpdateCredentialRequest).GetProperty(nameof(UpdateCredentialRequest.IsEnabled))!
            .PropertyType.Should().Be(typeof(bool?));

        var declaredProperties = typeof(UpdateCredentialRequest)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Select(property => property.Name)
            .ToList();

        declaredProperties.Should().NotContain([
            "Password",
            "PasswordByte",
            "AvatarStorageFileId",
            "AvatarUrl",
            "TenantId",
            "IsDeleted",
            "CreatedAt",
            "ModifiedAt"
        ]);
    }

    [TestCase(typeof(CreateCredentialEndpoint), "HandleHttp")]
    [TestCase(typeof(CreateTenantEndpoint), "HandleHttp")]
    [TestCase(typeof(UpdateTenantEndpoint), "HandleHttp")]
    [TestCase(typeof(CreateVerificationEndpoint), "Handle")]
    public void TouchedHttpEndpoints_AreIncludedInOpenApi(Type endpointType, string methodName)
    {
        var endpoint = endpointType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
        endpoint.Should().NotBeNull();

        endpoint!.GetCustomAttributes<MapEndpointAttribute>().Single()
            .ExcludeFromOpenApi.Should().BeFalse();
    }

    [Test]
    public void UpdateCredentialEndpoint_IsIncludedInOpenApi()
    {
        var builder = WebApplication.CreateBuilder();
        using var app = builder.Build();
        app.MapUpdateCredentialEndpoint();

        var endpoint = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(static source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Single(item => item.RoutePattern.RawText == "/api/credentials/{id:guid}");

        var isExcluded = endpoint.Metadata.Any(static metadata =>
        {
            var property = metadata.GetType().GetProperty("ExcludeFromDescription");
            return property is not null && Equals(property.GetValue(metadata), true);
        });
        isExcluded.Should().BeFalse();
    }

    private static void AssertBoltResponse<TRequest, TResponse>()
    {
        typeof(TRequest).GetInterfaces().Should().Contain(typeof(IBoltRequest<TRequest, TResponse>));
    }

    private static void AssertScalarContract<TResponse>()
    {
        typeof(TResponse)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => !IsScalar(property.PropertyType))
            .Select(property => property.Name)
            .Should().BeEmpty($"{typeof(TResponse).Name} must not expose EF entities or navigation collections");
    }

    private static bool IsScalar(Type type)
    {
        var scalarType = Nullable.GetUnderlyingType(type) ?? type;
        return scalarType.IsEnum
               || scalarType.IsPrimitive
               || scalarType == typeof(string)
               || scalarType == typeof(Guid)
               || scalarType == typeof(decimal)
               || scalarType == typeof(DateTime)
               || scalarType == typeof(DateTimeOffset);
    }
}
