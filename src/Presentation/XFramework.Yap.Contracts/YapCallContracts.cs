namespace Yap.Contracts;

public sealed record StartYapCall(Guid ThreadId, Guid RecipientId);
public sealed record YapCallInvite(Guid Id, Guid ThreadId, Guid CallerId, string CallerName,
    Guid RecipientId, DateTimeOffset ExpiresAt);
public sealed record YapCallConnection(Guid CallId, string Url, string ClientId, string RecipientClientId);
public sealed record YapCallEvent(string Type, YapCallInvite Invite, Guid? CredentialId = null,
    YapGroupCall? Group = null, YapGroupControlEvent? Control = null);

public sealed record StartYapGroupCall(Guid ThreadId, Guid DeviceId, Guid[] Recipients);
public sealed record AcceptYapGroupCall(Guid DeviceId);
public sealed record YapGroupCall(Guid Id, Guid ThreadId, Guid CallerId, string CallerName,
    long Revision, DateTimeOffset ExpiresAt, YapGroupParticipant[] Participants);
/// <summary>Video is roster state, exactly like Muted: it says whether that participant's camera is on,
/// never what it is sending. The picture itself is end-to-end encrypted and the relay cannot see it.</summary>
public sealed record YapGroupParticipant(Guid CredentialId, Guid DeviceId, bool Accepted, bool Ready, bool Left, bool Muted, bool Video = false);
public sealed record YapGroupControl(long Revision, long Sequence, Guid RecipientId, string Envelope, string Kind = "key");
public sealed record YapGroupControlEvent(Guid CallId, long Revision, long Sequence, Guid SenderId,
    Guid SenderDeviceId, Guid RecipientId, string Envelope, string Kind = "key");
public sealed record YapGroupMute(bool Muted);
public sealed record YapGroupVideo(bool Video);
public sealed record YapGroupReady(long Revision);
