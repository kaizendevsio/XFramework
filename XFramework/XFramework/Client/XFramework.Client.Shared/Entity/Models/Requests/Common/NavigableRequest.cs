namespace XFramework.Client.Shared.Entity.Models.Requests.Common;

public class NavigableRequest
{
    public Action? OnSuccess { get; set; }
    public Action? OnFailure { get; set; }
    public bool Silent { get; set; }
    public string NavigateToOnSuccess { get; set; }
    public string NavigateToOnFailure { get; set; }
    public string NavigateToOnVerificationRequired { get; set; }
}