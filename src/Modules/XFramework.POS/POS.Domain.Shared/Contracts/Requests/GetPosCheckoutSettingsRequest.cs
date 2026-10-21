namespace POS.Domain.Shared.Contracts.Requests;

[MemoryPackable]
public partial record GetPosCheckoutSettingsRequest : RequestBase,
    IBoltRequest<GetPosCheckoutSettingsRequest, QueryResponse<PosCheckoutSettingsResponse>>;
