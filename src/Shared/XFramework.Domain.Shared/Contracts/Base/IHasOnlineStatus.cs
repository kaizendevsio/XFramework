namespace XFramework.Domain.Shared.Contracts.Base;

public interface IHasOnlineStatus
{
    public bool IsOnline { get; set; }
    public DateTime LastSeen { get; set; }
    public DateTime? OnlineSince { get; set; }
    public string? StatusMessage { get; set; }
    public string? LastActivityType { get; set; }
    public string? Device { get; set; }
    // Location could be a complex type or a simple string representation
    public string? Location { get; set; } //TODO: Implement a complex type for location
}