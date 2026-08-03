using FluentAssertions;
using FluentValidation;
using IdentityServer.Api.Features.GeneratedEntityValidation;
using IdentityServer.Domain.Shared.Contracts;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace IdentityServer.UnitTests;

[TestFixture]
public sealed class GeneratedEntityValidatorTests
{
    [Test]
    public async Task IdentityAddressValidators_InvalidIdsLengthsAndCoordinates_AreRejected()
    {
        var request = new CreateIdentityAddressRequest
        {
            IdentityInfoId = Guid.Empty,
            BarangayId = Guid.Empty,
            Street = new string('s', 501),
            Latitude = 90.01,
            Longitude = -180.01
        };

        var createResult = await new CreateIdentityAddressRequestValidator().ValidateAsync(request);
        var updateResult = await new UpdateIdentityAddressRequestValidator().ValidateAsync(
            new UpdateIdentityAddressRequest
            {
                IdentityInfoId = request.IdentityInfoId,
                BarangayId = request.BarangayId,
                Street = request.Street,
                Latitude = request.Latitude,
                Longitude = request.Longitude
            });
        var entityResult = await new IdentityAddressValidator().ValidateAsync(
            new IdentityAddress
            {
                IdentityInfoId = request.IdentityInfoId,
                BarangayId = request.BarangayId,
                Street = request.Street,
                Latitude = request.Latitude,
                Longitude = request.Longitude
            });

        createResult.IsValid.Should().BeFalse();
        updateResult.IsValid.Should().BeFalse();
        entityResult.IsValid.Should().BeFalse();
        createResult.Errors.Select(error => error.PropertyName).Should().Contain(
            [nameof(request.IdentityInfoId), nameof(request.BarangayId), nameof(request.Street), nameof(request.Latitude), nameof(request.Longitude)]);
    }

    [Test]
    public async Task IdentityContactValidators_EmptyRelationshipsAndLongValue_AreRejected()
    {
        var request = new CreateIdentityContactRequest
        {
            TypeId = Guid.Empty,
            CredentialId = Guid.Empty,
            GroupId = Guid.Empty,
            Value = new string('v', 501)
        };

        var createResult = await new CreateIdentityContactRequestValidator().ValidateAsync(request);
        var updateResult = await new UpdateIdentityContactRequestValidator().ValidateAsync(
            new UpdateIdentityContactRequest
            {
                TypeId = request.TypeId,
                CredentialId = request.CredentialId,
                GroupId = request.GroupId,
                Value = request.Value
            });
        var entityResult = await new IdentityContactValidator().ValidateAsync(
            new IdentityContact
            {
                TypeId = request.TypeId,
                CredentialId = request.CredentialId,
                GroupId = request.GroupId,
                Value = request.Value
            });

        createResult.IsValid.Should().BeFalse();
        updateResult.IsValid.Should().BeFalse();
        entityResult.IsValid.Should().BeFalse();
        createResult.Errors.Select(error => error.PropertyName).Should().Contain(
            [nameof(request.TypeId), nameof(request.CredentialId), nameof(request.GroupId), nameof(request.Value)]);
    }

    [Test]
    public async Task IdentityFavoriteValidators_InvalidRelationshipsAndLongData_AreRejected()
    {
        var request = new CreateIdentityFavoriteRequest
        {
            FavoriteTypeId = Guid.Empty,
            CredentialId = Guid.Empty,
            Data = new string('d', 5001)
        };

        var createResult = await new CreateIdentityFavoriteRequestValidator().ValidateAsync(request);
        var updateResult = await new UpdateIdentityFavoriteRequestValidator().ValidateAsync(
            new UpdateIdentityFavoriteRequest
            {
                FavoriteTypeId = request.FavoriteTypeId,
                CredentialId = request.CredentialId,
                Data = request.Data
            });
        var entityResult = await new IdentityFavoriteValidator().ValidateAsync(
            new IdentityFavorite
            {
                FavoriteTypeId = request.FavoriteTypeId,
                CredentialId = request.CredentialId,
                Data = request.Data
            });

        createResult.IsValid.Should().BeFalse();
        updateResult.IsValid.Should().BeFalse();
        entityResult.IsValid.Should().BeFalse();
        createResult.Errors.Select(error => error.PropertyName).Should().Contain(
            [nameof(request.FavoriteTypeId), nameof(request.CredentialId), nameof(request.Data)]);
    }

    [Test]
    public async Task RegistryConfigurationValidators_InvalidRequiredFieldsAndLengths_AreRejected()
    {
        var request = new CreateRegistryConfigurationRequest
        {
            Key = new string('k', 201),
            GroupId = Guid.Empty,
            Unit = new string('u', 101),
            Value = new string('v', 5001)
        };

        var createResult = await new CreateRegistryConfigurationRequestValidator().ValidateAsync(request);
        var updateResult = await new UpdateRegistryConfigurationRequestValidator().ValidateAsync(
            new UpdateRegistryConfigurationRequest
            {
                Key = request.Key,
                GroupId = request.GroupId,
                Unit = request.Unit,
                Value = request.Value
            });
        var entityResult = await new RegistryConfigurationValidator().ValidateAsync(
            new RegistryConfiguration
            {
                Key = request.Key,
                GroupId = request.GroupId,
                Unit = request.Unit,
                Value = request.Value
            });

        createResult.IsValid.Should().BeFalse();
        updateResult.IsValid.Should().BeFalse();
        entityResult.IsValid.Should().BeFalse();
        createResult.Errors.Select(error => error.PropertyName).Should().Contain(
            [nameof(request.Key), nameof(request.GroupId), nameof(request.Unit), nameof(request.Value)]);
    }

    [Test]
    public void ValidatorAssemblyScanning_RegistersRequestAndEntityValidators()
    {
        var services = new ServiceCollection();
        services.AddValidatorsFromAssemblyContaining<IdentityAddressValidator>();

        using var provider = services.BuildServiceProvider();

        Type[] validatorServiceTypes =
        [
            typeof(IValidator<IdentityAddress>),
            typeof(IValidator<CreateIdentityAddressRequest>),
            typeof(IValidator<UpdateIdentityAddressRequest>),
            typeof(IValidator<IdentityContact>),
            typeof(IValidator<CreateIdentityContactRequest>),
            typeof(IValidator<UpdateIdentityContactRequest>),
            typeof(IValidator<IdentityFavorite>),
            typeof(IValidator<CreateIdentityFavoriteRequest>),
            typeof(IValidator<UpdateIdentityFavoriteRequest>),
            typeof(IValidator<RegistryConfiguration>),
            typeof(IValidator<CreateRegistryConfigurationRequest>),
            typeof(IValidator<UpdateRegistryConfigurationRequest>),
            typeof(IValidator<RegistryConfigurationGroup>)
        ];

        validatorServiceTypes.Should().AllSatisfy(
            serviceType => provider.GetService(serviceType).Should().NotBeNull());
    }
}
