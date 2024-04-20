using XFramework.Domain.Shared.Contracts.Base;

namespace XFramework.Domain.Shared.Contracts;

public partial class CommunityIdentity : BaseModel
{
    public Guid CredentialId { get; set; }

    public string? HandleName { get; set; }

    public int Status { get; set; }

    public DateTime LastActive { get; set; }

    public Guid TypeId { get; set; }

    public string? Alias { get; set; }

    public string? Tagline { get; set; }

    public virtual ICollection<CommunityConnection> CommunityConnectionSourceSocialMediaIdentities { get; set; } =
        new List<CommunityConnection>();

    public virtual ICollection<CommunityConnection> CommunityConnectionTargetSocialMediaIdentities { get; set; } =
        new List<CommunityConnection>();

    public virtual ICollection<CommunityContent> CommunityContentCommunityGroups { get; set; } =
        new List<CommunityContent>();

    public virtual ICollection<CommunityContentReaction> CommunityContentReactions { get; set; } =
        new List<CommunityContentReaction>();

    public virtual ICollection<CommunityContent> CommunityContentSocialMediaIdentities { get; set; } =
        new List<CommunityContent>();

    public virtual ICollection<CommunityIdentityFile> CommunityIdentityFiles { get; set; } =
        new List<CommunityIdentityFile>();

    public virtual CommunityIdentityType Type { get; set; } = null!;

    public virtual IdentityCredential Credential { get; set; } = null!;
}