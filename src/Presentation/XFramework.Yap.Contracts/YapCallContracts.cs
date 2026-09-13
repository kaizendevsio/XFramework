namespace Yap.Contracts;

public sealed record StartYapCall(Guid ThreadId, Guid RecipientId);
public sealed record YapCallInvite(Guid Id, Guid ThreadId, Guid CallerId, string CallerName,
    Guid RecipientId, DateTimeOffset ExpiresAt);
public sealed record YapCallConnection(Guid CallId, string Url, string ClientId, string RecipientClientId);
public sealed record YapCallEvent(string Type, YapCallInvite Invite, Guid? CredentialId = null);
