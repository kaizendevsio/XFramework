namespace Coins.Api.Configurations;

/// <summary>
/// Configuration for Bitcoin blockchain operations
/// </summary>
public class BtcBlockchainConfiguration
{
    public WalletConfiguration WalletConfiguration { get; set; } = new();
    
    public Uri? ApiUrl { get; set; }
    public Uri? ServiceUrl { get; set; }
    public Uri? PaymentCallbackUrl { get; set; }
    public Uri? FeeUrl { get; set; }
    
    public string ApiCode { get; set; } = string.Empty;
    public int MinConfirmations { get; set; }
    public int MaxGapLimit { get; set; }
    public decimal GapPaymentAmount { get; set; }
}

/// <summary>
/// Bitcoin wallet configuration
/// </summary>
public class WalletConfiguration
{
    public string PublicKey { get; set; } = string.Empty;
    public string WalletIdentifier { get; set; } = string.Empty;
    public string WalletPassword { get; set; } = string.Empty;
}