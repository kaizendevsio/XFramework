namespace Community.Domain.Shared.Contracts.Responses;

[MemoryPackable]
public partial record GetContentResponse
{
    public Guid Id { get; set; }
    public string? Title { get; set; }
    public string? Text { get; set; }
    public Guid SocialMediaIdentityId { get; set; }
    public string? AuthorHandleName { get; set; }
    public string? AuthorAlias { get; set; }
    public Guid TypeId { get; set; }
    public Guid? ParentContentId { get; set; }
    public int ReactionCount { get; set; }
    public int CommentCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }
}
