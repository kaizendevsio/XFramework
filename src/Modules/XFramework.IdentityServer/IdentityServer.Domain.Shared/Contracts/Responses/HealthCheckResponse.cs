namespace IdentityServer.Domain.Shared.Contracts.Responses;

[MemoryPackable]
public partial record HealthCheckResponse
{
    public string Status { get; set; } = "ok";
    public long Timestamp { get; set; }
}
