namespace Communications.Domain.Shared.Contracts.Requests.ReferenceData;

[MemoryPackable]
public partial record EnsureChatDefaultsRequest : RequestBase,
    ICommand<QueryResponse<ChatReferenceDataResponse>>,
    IBoltRequest<EnsureChatDefaultsRequest, QueryResponse<ChatReferenceDataResponse>>;
