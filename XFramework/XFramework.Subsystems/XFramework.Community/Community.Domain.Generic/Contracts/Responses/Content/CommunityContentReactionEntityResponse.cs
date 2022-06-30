namespace Community.Domain.Generic.Contracts.Responses.Content;

public class CommunityContentReactionEntityResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public string? Name { get; set; }
    public string? Emoji { get; set; }
    public Guid? Guid { get; set; }
}