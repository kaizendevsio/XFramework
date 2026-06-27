using Storage.Domain.Shared.Contracts.Responses;

namespace Storage.Domain.Shared.Contracts.Requests;

[MemoryPackable]
public partial record CleanupStorageRetentionRequest : RequestBase,
    ICommand<QueryResponse<StorageRetentionCleanupResponse>>,
    IBoltRequest<CleanupStorageRetentionRequest, QueryResponse<StorageRetentionCleanupResponse>>
{
    public int MaxFiles { get; set; } = 100;
    public bool DryRun { get; set; }
}
