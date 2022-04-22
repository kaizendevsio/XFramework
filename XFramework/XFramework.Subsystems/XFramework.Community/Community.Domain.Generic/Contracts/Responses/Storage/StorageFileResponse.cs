namespace Community.Domain.Generic.Contracts.Responses.Storage;

public class StorageFileResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public string? ContentPath { get; set; }
    public string? Guid { get; set; }
    
    public virtual StorageFileEntityResponse? Entity { get; set; }
}