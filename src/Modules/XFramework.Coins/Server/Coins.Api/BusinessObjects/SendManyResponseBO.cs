namespace Coins.Api.BusinessObjects;

/// <summary>
/// Response from send many blockchain operation
/// </summary>
public class SendManyResponseBO
{
    public List<long> IDs { get; set; }
    public List<string> To { get; set; }
    public List<decimal> Amounts { get; set; }
    public List<string> From { get; set; }
    public decimal Fee { get; set; }
    public string Txid { get; set; } = string.Empty;
    public string Tx_Hash { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Warning { get; set; } = string.Empty;

    public SendManyResponseBO()
    {
        IDs = new List<long>();
        To = new List<string>();
        Amounts = new List<decimal>();
        From = new List<string>();
    }
}