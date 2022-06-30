namespace XFramework.Client.Shared.Entity.Models.Responses.CoinCap;

public class AssetListResponse
{
    [JsonPropertyName("data")]
    public List<AssetResponse> Data { get; set; }

    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }
}