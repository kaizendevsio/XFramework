namespace POS.Domain.Shared.Contracts.Requests;

[MemoryPackable]
public partial record CreatePosScannerPairingRequest : RequestBase,
    ICommand<CmdResponse<PosScannerPairingResponse>>,
    IBoltRequest<CreatePosScannerPairingRequest, CmdResponse<PosScannerPairingResponse>>
{
    public Guid RegisterId { get; set; }
}

[MemoryPackable]
public partial record ClaimPosScannerPairingRequest : RequestBase,
    ICommand<CmdResponse<PosScannerPhoneResponse>>,
    IBoltRequest<ClaimPosScannerPairingRequest, CmdResponse<PosScannerPhoneResponse>>
{
    public string Challenge { get; set; } = "";
}

[MemoryPackable]
public partial record SendPosScannerCodeRequest : RequestBase,
    ICommand<CmdResponse<PosScannerSendResponse>>,
    IBoltRequest<SendPosScannerCodeRequest, CmdResponse<PosScannerSendResponse>>
{
    public Guid PairingId { get; set; }
    public string PhoneKey { get; set; } = "";
    public long Sequence { get; set; }
    public string Code { get; set; } = "";
}

[MemoryPackable]
public partial record PollPosScannerCodesRequest : RequestBase,
    IQuery<QueryResponse<PosScannerPollResponse>>,
    IBoltRequest<PollPosScannerCodesRequest, QueryResponse<PosScannerPollResponse>>
{
    public Guid PairingId { get; set; }
    public string DesktopKey { get; set; } = "";
    public long AcknowledgedSequence { get; set; }
    public bool PauseDelivery { get; set; }
}

[MemoryPackable]
public partial record RevokePosScannerPairingRequest : RequestBase,
    ICommand<CmdResponse<bool>>,
    IBoltRequest<RevokePosScannerPairingRequest, CmdResponse<bool>>
{
    public Guid PairingId { get; set; }
    public string DesktopKey { get; set; } = "";
}

[MemoryPackable]
public partial record GetPosScannerStatusRequest : RequestBase,
    IQuery<QueryResponse<PosScannerPhoneResponse>>,
    IBoltRequest<GetPosScannerStatusRequest, QueryResponse<PosScannerPhoneResponse>>
{
    public Guid PairingId { get; set; }
    public string PhoneKey { get; set; } = "";
}
