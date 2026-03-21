namespace XFramework.Integration.DataContext.Cache;

public class CacheEntry
{
    public byte[] Data { get; set; } = [];
    public DateTime ExpiresAtUtc { get; set; }
    public string EntityTypeName { get; set; } = string.Empty;

    public bool IsExpired => DateTime.UtcNow >= ExpiresAtUtc;
}
