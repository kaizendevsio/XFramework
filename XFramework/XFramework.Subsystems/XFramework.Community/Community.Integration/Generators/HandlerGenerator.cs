using XFramework.Domain.Generic.Contracts;
using XFramework.Integration.Attributes;

namespace IdentityServer.Integration.Generators;

[StreamFlowWrapper("XFramework.Domain.Generic.Contracts",new[] {
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
public static class CommunityServiceWrapper;