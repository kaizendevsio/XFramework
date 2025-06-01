namespace Payments.Domain.Shared.Contracts;

[MemoryPackable]
public partial record MerchantCredentials
{
    public string MerchantId { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ApiSecret { get; set; } = string.Empty;
    public Dictionary<string, string> AdditionalProperties { get; set; } = new();
}
