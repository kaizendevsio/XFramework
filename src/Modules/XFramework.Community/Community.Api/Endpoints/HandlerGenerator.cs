using XFramework.Core.Attributes;

namespace Community.Api.Endpoints;

[GenerateEndpoints("XFramework.Domain.Shared.Contracts",new[] {
    nameof(CommunityIdentity),
    nameof(CommunityIdentityType),
    nameof(CommunityIdentityFile),
    nameof(CommunityIdentityFileType),
    nameof(CommunityConnection),
    nameof(CommunityConnectionType),
    nameof(CommunityContent),
    nameof(CommunityContentType),
    nameof(CommunityContentFile),
    nameof(CommunityContentReaction),
    nameof(CommunityContentReactionType)
})]
public static partial class CommunityEndpoints;