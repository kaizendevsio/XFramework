namespace Coins.Api.BusinessObjects;

/// <summary>
/// Bitcoin transaction business object
/// </summary>
public class BtcTransactionBO
{
    public long Id { get; set; }
    public string BtcAddress { get; set; } = string.Empty;
    public decimal BtcAmount { get; set; }
}