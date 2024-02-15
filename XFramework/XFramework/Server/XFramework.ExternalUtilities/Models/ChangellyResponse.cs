namespace XFramework.External.Models;

public class ChangellyResponse
{
    public string jsonrpc { get; set; }
    public string id { get; set; }
    public ChangellyTransactionResponse result { get; set; }
}