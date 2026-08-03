using System.Linq.Expressions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace IdentityServer.Api.Features.GeneratedEntityValidation;

public abstract class IdentityAddressValidatorBase<T> : AbstractValidator<T>
{
    protected IdentityAddressValidatorBase(
        Expression<Func<T, Guid>> identityInfoId,
        Expression<Func<T, string?>> unitNumber,
        Expression<Func<T, string?>> street,
        Expression<Func<T, string?>> building,
        Expression<Func<T, string?>> name,
        Expression<Func<T, Guid?>> barangayId,
        Expression<Func<T, Guid?>> cityId,
        Expression<Func<T, string?>> subdivision,
        Expression<Func<T, Guid?>> regionId,
        Expression<Func<T, Guid?>> addressTypeId,
        Expression<Func<T, Guid?>> provinceId,
        Expression<Func<T, Guid?>> countryId,
        Expression<Func<T, double?>> latitude,
        Expression<Func<T, double?>> longitude,
        Expression<Func<T, string?>> consolidatedName)
    {
        RuleFor(identityInfoId).NotEmpty();
        RuleFor(unitNumber).MaximumLength(500);
        RuleFor(street).MaximumLength(500);
        RuleFor(building).MaximumLength(500);
        RuleFor(name).MaximumLength(500);
        OptionalIdentifier(barangayId);
        OptionalIdentifier(cityId);
        RuleFor(subdivision).MaximumLength(500);
        OptionalIdentifier(regionId);
        OptionalIdentifier(addressTypeId);
        OptionalIdentifier(provinceId);
        OptionalIdentifier(countryId);
        RuleFor(latitude).InclusiveBetween(-90, 90);
        RuleFor(longitude).InclusiveBetween(-180, 180);
        RuleFor(consolidatedName).MaximumLength(1000);
    }

    private void OptionalIdentifier(Expression<Func<T, Guid?>> selector) =>
        RuleFor(selector)
            .Must(id => !id.HasValue || id.Value != Guid.Empty)
            .WithMessage("{PropertyName} must be a valid identifier when provided.");
}

public sealed class IdentityAddressValidator : IdentityAddressValidatorBase<IdentityAddress>
{
    public IdentityAddressValidator(DbContext? dbContext = null)
        : base(
            x => x.IdentityInfoId,
            x => x.UnitNumber,
            x => x.Street,
            x => x.Building,
            x => x.Name,
            x => x.BarangayId,
            x => x.CityId,
            x => x.Subdivision,
            x => x.RegionId,
            x => x.AddressTypeId,
            x => x.ProvinceId,
            x => x.CountryId,
            x => x.Latitude,
            x => x.Longitude,
            x => x.ConsolidatedName)
    {
        if (dbContext is null)
            return;

        RuleFor(x => x.IdentityInfoId).MustAsync(
            (address, id, ct) => TenantRelationshipValidation.ExistsAsync<IdentityInformation>(
                dbContext, id, address.TenantId, TenantRelationshipValidation.TenantScope.TenantOnly, ct));
        RuleFor(x => x.BarangayId).MustAsync(
            (address, id, ct) => TenantRelationshipValidation.OptionalExistsAsync<AddressBarangay>(
                dbContext, id, address.TenantId, TenantRelationshipValidation.TenantScope.TenantOnly, ct));
        RuleFor(x => x.CityId).MustAsync(
            (address, id, ct) => TenantRelationshipValidation.OptionalExistsAsync<AddressCity>(
                dbContext, id, address.TenantId, TenantRelationshipValidation.TenantScope.TenantOnly, ct));
        RuleFor(x => x.RegionId).MustAsync(
            (address, id, ct) => TenantRelationshipValidation.OptionalExistsAsync<AddressRegion>(
                dbContext, id, address.TenantId, TenantRelationshipValidation.TenantScope.TenantOnly, ct));
        RuleFor(x => x.AddressTypeId).MustAsync(
            (address, id, ct) => TenantRelationshipValidation.OptionalExistsAsync<IdentityAddressType>(
                dbContext, id, address.TenantId, TenantRelationshipValidation.TenantScope.TenantOrGlobal, ct));
        RuleFor(x => x.ProvinceId).MustAsync(
            (address, id, ct) => TenantRelationshipValidation.OptionalExistsAsync<AddressProvince>(
                dbContext, id, address.TenantId, TenantRelationshipValidation.TenantScope.TenantOnly, ct));
        RuleFor(x => x.CountryId).MustAsync(
            (address, id, ct) => TenantRelationshipValidation.OptionalExistsAsync<AddressCountry>(
                dbContext, id, address.TenantId, TenantRelationshipValidation.TenantScope.TenantOnly, ct));
    }
}

public sealed class CreateIdentityAddressRequestValidator : IdentityAddressValidatorBase<CreateIdentityAddressRequest>
{
    public CreateIdentityAddressRequestValidator()
        : base(
            x => x.IdentityInfoId,
            x => x.UnitNumber,
            x => x.Street,
            x => x.Building,
            x => x.Name,
            x => x.BarangayId,
            x => x.CityId,
            x => x.Subdivision,
            x => x.RegionId,
            x => x.AddressTypeId,
            x => x.ProvinceId,
            x => x.CountryId,
            x => x.Latitude,
            x => x.Longitude,
            x => x.ConsolidatedName)
    {
    }
}

public sealed class UpdateIdentityAddressRequestValidator : IdentityAddressValidatorBase<UpdateIdentityAddressRequest>
{
    public UpdateIdentityAddressRequestValidator()
        : base(
            x => x.IdentityInfoId,
            x => x.UnitNumber,
            x => x.Street,
            x => x.Building,
            x => x.Name,
            x => x.BarangayId,
            x => x.CityId,
            x => x.Subdivision,
            x => x.RegionId,
            x => x.AddressTypeId,
            x => x.ProvinceId,
            x => x.CountryId,
            x => x.Latitude,
            x => x.Longitude,
            x => x.ConsolidatedName)
    {
    }
}
