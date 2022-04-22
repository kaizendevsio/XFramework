namespace Community.Domain.Generic.Contracts.Responses.Content;

public class CommunityContentFileResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public long ContentId { get; set; }
    public long StorageId { get; set; }
    public string Guid { get; set; }
}