namespace XFramework.Domain.Shared.DataContext;

public record CachePolicy
{
    public TimeSpan AbsoluteExpiration { get; init; } = TimeSpan.FromMinutes(5);
    public bool Enabled { get; init; }
}
