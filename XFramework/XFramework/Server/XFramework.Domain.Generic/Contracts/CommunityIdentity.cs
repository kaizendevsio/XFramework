namespace XFramework.Domain.Generic.Contracts;

public partial class CommunityIdentity : BaseModel
{
    public Guid CredentialId { get; set; }

    public string? HandleName { get; set; }

    public int Status { get; set; }

    public DateTime LastActive { get; set; }

    public Guid TypeId { get; set; }

    public string? Alias { get; set; }

    public string? Tagline { get; set; }

    public virtual ICollection<CommunityConnection> CommunityConnectionSourceSocialMediaIdentities { get; } =
        new List<CommunityConnection>();

    public virtual ICollection<CommunityConnection> CommunityConnectionTargetSocialMediaIdentities { get; } =
        new List<CommunityConnection>();

    public virtual ICollection<CommunityContent> CommunityContentCommunityGroups { get; } =
        new List<CommunityContent>();

    public virtual ICollection<CommunityContentReaction> CommunityContentReactions { get; } =
        new List<CommunityContentReaction>();

    public virtual ICollection<CommunityContent> CommunityContentSocialMediaIdentities { get; } =
        new List<CommunityContent>();

    public virtual ICollection<CommunityIdentityFile> CommunityIdentityFiles { get; } =
        new List<CommunityIdentityFile>();

    public virtual CommunityIdentityType Type { get; set; } = null!;

    public virtual IdentityCredential Credential { get; set; } = null!;
}