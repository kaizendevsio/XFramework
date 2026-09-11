namespace Communications.Domain.Shared.Contracts.Requests.ReferenceData;

[MemoryPackable]
public partial record GetChatReferenceDataRequest : RequestBase,
    IQuery<QueryResponse<ChatReferenceDataResponse>>,
    IBoltRequest<GetChatReferenceDataRequest, QueryResponse<ChatReferenceDataResponse>>;
