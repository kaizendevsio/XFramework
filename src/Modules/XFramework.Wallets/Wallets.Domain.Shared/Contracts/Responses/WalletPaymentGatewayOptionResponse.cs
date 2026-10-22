namespace Wallets.Domain.Shared.Contracts.Responses;

[MemoryPackable]
public partial record WalletPaymentGatewayOptionResponse
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public Guid GatewayCategoryId { get; init; }
    public string? CategoryName { get; init; }
    public decimal ServiceCharge { get; init; }
    public decimal ConvenienceFee { get; init; }
    public decimal? Discount { get; init; }
}
