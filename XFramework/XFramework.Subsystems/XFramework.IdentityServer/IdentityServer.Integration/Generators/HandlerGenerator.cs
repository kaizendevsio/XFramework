using XFramework.Domain.Generic.Contracts;
using XFramework.Integration.Attributes;

namespace IdentityServer.Integration.Generators;

[StreamFlowWrapper("XFramework.Domain.Generic.Contracts", new[]
{
    nameof(IdentityInformation),
    nameof(IdentityCredential),
    nameof(IdentityAddress),
    nameof(IdentityAddressType),
    nameof(IdentityFavorite),
    nameof(IdentityVerification),
    nameof(IdentityVerificationType),
    nameof(IdentityContact),
    nameof(IdentityContactType),
    nameof(IdentityContactGroup),
    nameof(IdentityRole),
    nameof(IdentityRoleType),
    nameof(IdentityRoleTypeGroup),
    nameof(StorageFile),
    nameof(StorageFileType)
})]
public static class IdentityServerServiceWrapper;

[StreamFlowWrapper("XFramework.Domain.Generic.Contracts", new[]
{
    nameof(Tenant)
})]
public static class TenantServiceWrapper;

[StreamFlowWrapper("XFramework.Domain.Generic.Contracts", new[]
{
    nameof(AddressBarangay),
    nameof(AddressCity),
    nameof(AddressCountry),
    nameof(AddressProvince),
    nameof(AddressRegion)
})]
public static class AddressServiceWrapper;