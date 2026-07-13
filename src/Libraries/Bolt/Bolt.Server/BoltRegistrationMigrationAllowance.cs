namespace Bolt.Server;

public sealed class BoltRegistrationMigrationAllowance
{
    public string AuthenticatedServiceName { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAtUtc { get; set; }
}
