namespace Wallets.Domain.Shared.Contracts.Requests;

[MemoryPackable]
public partial record BatchIncrementWalletRequest : RequestBase,
    ICommand<CmdResponse<BatchOperationResult>>,
    IBoltRequest<BatchIncrementWalletRequest, CmdResponse<BatchOperationResult>>
{
    public List<BatchIncrementRequest> Requests { get; init; } = [];
    public bool AllowPartialSuccess { get; init; }
}

[MemoryPackable]
public partial record BatchDecrementWalletRequest : RequestBase,
    ICommand<CmdResponse<BatchOperationResult>>,
    IBoltRequest<BatchDecrementWalletRequest, CmdResponse<BatchOperationResult>>
{
    public List<BatchDecrementRequest> Requests { get; init; } = [];
    public bool AllowPartialSuccess { get; init; }
}

[MemoryPackable]
public partial record BatchTransferWalletRequest : RequestBase,
    ICommand<CmdResponse<BatchOperationResult>>,
    IBoltRequest<BatchTransferWalletRequest, CmdResponse<BatchOperationResult>>
{
    public List<BatchTransferRequest> Requests { get; init; } = [];
    public bool AllowPartialSuccess { get; init; }
}
