namespace Community.Domain.Shared.Contracts.Responses;

[MemoryPackable]
public partial record FeedItemResponse
{
    public Guid ContentId { get; set; }
    public string? Title { get; set; }
    public string? Text { get; set; }
    public Guid AuthorIdentityId { get; set; }
    public string? AuthorHandleName { get; set; }
    public string? AuthorAlias { get; set; }
    public Guid ContentTypeId { get; set; }
    public int ReactionCount { get; set; }
    public int CommentCount { get; set; }
    public DateTime CreatedAt { get; set; }
}
