﻿using XFramework.Core.Attributes;

namespace IdentityServer.Api.Endpoints;

[GenerateEndpoints("XFramework.Domain.Shared.Contracts",new[] {
    nameof(AddressBarangay),
    nameof(AddressCity),
    nameof(AddressCountry),
    nameof(AddressProvince),
    nameof(AddressRegion),
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
    nameof(RegistryConfiguration),
    nameof(RegistryConfigurationGroup),
    nameof(StorageFile),
    nameof(StorageFileType),
    nameof(Tenant)
})]
public static partial class IdentityServerEndpoints;