namespace XFramework.Bolt.Phase0Synthetics;

public sealed record SyntheticOptions(
    Uri Target,
    Guid TenantId,
    Guid CredentialId,
    string DeviceId,
    SecretToken CommunicationsToken,
    SecretToken UserToken,
    TimeSpan OperationTimeout,
    SecretToken? ExpiryToken,
    TimeSpan ExpiryGrace,
    TimeSpan ExpiryMaxWait,
    SecretToken? RejectedCommunicationsToken,
    SecretToken? RejectedUserToken);
