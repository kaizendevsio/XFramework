using XFramework.Domain.Generic.Contracts;
using XFramework.Integration.Attributes;

namespace IdentityServer.Integration.Generators;

[GenerateStreamFlowWrapper("XFramework.Domain.Generic.Contracts",new[] {
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
public static partial class IdentityServerServiceWrapper;