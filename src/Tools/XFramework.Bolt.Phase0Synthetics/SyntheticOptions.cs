namespace XFramework.Bolt.Phase0Synthetics;

public sealed record SyntheticOptions(
    Uri Target,
    Guid TenantId,
    Guid CredentialId,
    string DeviceId,
    SecretToken CommunicationsTransportToken,
    SecretToken PortalTransportToken,
    SecretToken PortalIdentityServiceToken,
    SecretToken UserActorToken,
    TimeSpan OperationTimeout,
    SecretToken? ExpiryTransportToken,
    TimeSpan ExpiryGrace,
    TimeSpan ExpiryMaxWait,
    SecretToken? RejectedCommunicationsTransportToken,
    SecretToken? RejectedPortalTransportToken);
