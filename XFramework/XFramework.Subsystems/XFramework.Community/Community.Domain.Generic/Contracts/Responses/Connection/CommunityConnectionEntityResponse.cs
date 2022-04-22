namespace Community.Domain.Generic.Contracts.Responses.Connection;

public class CommunityConnectionEntityResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public string Name { get; set; }
    public string Guid { get; set; }
}