namespace Communications.Domain.Shared.Contracts.Requests.Threads;

[MemoryPackable]
public partial record CompleteDeferredEncryptionRequest : RequestBase,
    ICommand<CmdResponse>, IBoltRequest<CompleteDeferredEncryptionRequest, CmdResponse>
{
    public Guid ThreadId { get; set; }
    public Guid MessageId { get; set; }
    public string ExpectedEnvelopeHash { get; set; } = "";
    public string EncryptedEnvelope { get; set; } = "";
    public Guid EncryptionSenderDeviceId { get; set; }
    public long SenderDirectoryRevision { get; set; }
    public Dictionary<Guid, long> RecipientDirectoryRevisions { get; set; } = [];
}
