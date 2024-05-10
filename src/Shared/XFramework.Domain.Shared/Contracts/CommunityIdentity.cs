namespace XFramework.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
public partial class CommunityIdentity : BaseModel
{
    
    [MemoryPackOrder(0)]
    public Guid CredentialId { get; set; }

    [MemoryPackOrder(1)]
    public string? HandleName { get; set; }

    [MemoryPackOrder(2)]
    public int Status { get; set; }

    [MemoryPackOrder(3)]
    public DateTime LastActive { get; set; }

    [MemoryPackOrder(4)]
    public Guid TypeId { get; set; }

    [MemoryPackOrder(5)]
    public string? Alias { get; set; }

    [MemoryPackOrder(6)]
    public string? Tagline { get; set; }

    [MemoryPackOrder(7)]
    public virtual ICollection<CommunityConnection> CommunityConnectionSourceSocialMediaIdentities { get; set; } =
        new List<CommunityConnection>();

    [MemoryPackOrder(8)]
    public virtual ICollection<CommunityConnection> CommunityConnectionTargetSocialMediaIdentities { get; set; } =
        new List<CommunityConnection>();

    [MemoryPackOrder(9)]
    public virtual ICollection<CommunityContent> CommunityContentCommunityGroups { get; set; } =
        new List<CommunityContent>();

    [MemoryPackOrder(10)]
    public virtual ICollection<CommunityContentReaction> CommunityContentReactions { get; set; } =
        new List<CommunityContentReaction>();

    [MemoryPackOrder(11)]
    public virtual ICollection<CommunityContent> CommunityContentSocialMediaIdentities { get; set; } =
        new List<CommunityContent>();

    [MemoryPackOrder(12)]
    public virtual ICollection<CommunityIdentityFile> CommunityIdentityFiles { get; set; } =
        new List<CommunityIdentityFile>();

    [MemoryPackOrder(13)]
    public virtual CommunityIdentityType Type { get; set; } = null!;

    [MemoryPackOrder(14)]
    public virtual IdentityCredential Credential { get; set; } = null!;
}
