using FluentAssertions;
using IdentityServer.Api.Features.Authorization.SetCredentialRolePermissionOverrides;
using IdentityServer.Api.Features.Authorization.SetRoleTypePermissions;
using IdentityServer.Api.Features.Tenants.SetModuleFeatures;
using IdentityServer.Domain.Shared.Contracts;
using IdentityServer.Domain.Shared.Contracts.Requests;
using IdentityServer.Domain.Shared.Contracts.Responses;
using IdentityServer.Domain.Shared.Enums;
using NUnit.Framework;

namespace IdentityServer.UnitTests;

[TestFixture]
public sealed class AuthorizationValidatorTests
{
    [Test]
    public void SetRoleTypePermissions_RejectsOversizedMatrix()
    {
        var request = new SetRoleTypePermissionsRequest
        {
            RoleTypeId = Guid.NewGuid(),
            ExpectedConcurrencyStamp = Guid.NewGuid(),
            Permissions = Enumerable.Range(0, 501).Select(_ => ValidPermission()).ToList()
        };

        new SetRoleTypePermissionsRequestValidator().Validate(request).IsValid.Should().BeFalse();
    }

    [Test]
    public void SetCredentialRoleOverrides_RejectsOversizedKeysAndMatrix()
    {
        var request = new SetCredentialRolePermissionOverridesRequest
        {
            IdentityRoleId = Guid.NewGuid(),
            ExpectedConcurrencyStamp = Guid.NewGuid(),
            Overrides = Enumerable.Range(0, 501).Select(_ => ValidPermission()).ToList()
        };
        request.Overrides[0].ModuleKey = new string('m', 129);
        request.Overrides[0].SubFeatureKey = new string('s', 129);

        var result = new SetCredentialRolePermissionOverridesRequestValidator().Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == "Overrides");
        result.Errors.Should().Contain(error => error.PropertyName.EndsWith("ModuleKey", StringComparison.Ordinal));
        result.Errors.Should().Contain(error => error.PropertyName.EndsWith("SubFeatureKey", StringComparison.Ordinal));
    }

    [Test]
    public void MatrixValidators_RejectNullCollectionsWithoutThrowing()
    {
        var rolePermissions = new SetRoleTypePermissionsRequest
        {
            RoleTypeId = Guid.NewGuid(),
            ExpectedConcurrencyStamp = Guid.NewGuid(),
            Permissions = null!
        };
        var overrides = new SetCredentialRolePermissionOverridesRequest
        {
            IdentityRoleId = Guid.NewGuid(),
            ExpectedConcurrencyStamp = Guid.NewGuid(),
            Overrides = null!
        };
        var features = new SetTenantModuleFeaturesRequest
        {
            TenantId = Guid.NewGuid(),
            ExpectedConcurrencyStamp = Guid.NewGuid(),
            Features = null!
        };

        new SetRoleTypePermissionsRequestValidator().Validate(rolePermissions).IsValid.Should().BeFalse();
        new SetCredentialRolePermissionOverridesRequestValidator().Validate(overrides).IsValid.Should().BeFalse();
        new SetTenantModuleFeaturesRequestValidator().Validate(features).IsValid.Should().BeFalse();
    }

    [Test]
    public void MatrixValidators_RejectNullElementsWithoutThrowing()
    {
        var rolePermissions = new SetRoleTypePermissionsRequest
        {
            RoleTypeId = Guid.NewGuid(),
            ExpectedConcurrencyStamp = Guid.NewGuid(),
            Permissions = [null!]
        };
        var overrides = new SetCredentialRolePermissionOverridesRequest
        {
            IdentityRoleId = Guid.NewGuid(),
            ExpectedConcurrencyStamp = Guid.NewGuid(),
            Overrides = [null!]
        };
        var features = new SetTenantModuleFeaturesRequest
        {
            TenantId = Guid.NewGuid(),
            ExpectedConcurrencyStamp = Guid.NewGuid(),
            Features = [null!]
        };

        new SetRoleTypePermissionsRequestValidator().Validate(rolePermissions).IsValid.Should().BeFalse();
        new SetCredentialRolePermissionOverridesRequestValidator().Validate(overrides).IsValid.Should().BeFalse();
        new SetTenantModuleFeaturesRequestValidator().Validate(features).IsValid.Should().BeFalse();
    }

    private static CapabilityPermissionDto ValidPermission() => new()
    {
        ModuleKey = "identity",
        SubFeatureKey = "credentials",
        CapabilityKey = "view",
        Effect = RoleCapabilityPermissionEffect.Allow
    };
}
