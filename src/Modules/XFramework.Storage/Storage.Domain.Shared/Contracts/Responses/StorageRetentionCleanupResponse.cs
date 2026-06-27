namespace Storage.Domain.Shared.Contracts.Responses;

[MemoryPackable]
public partial record StorageRetentionCleanupResponse
{
    public int MatchedCount { get; set; }
    public int DeletedObjectCount { get; set; }
    public bool DryRun { get; set; }
    public List<Guid> StorageFileIds { get; set; } = [];
}
