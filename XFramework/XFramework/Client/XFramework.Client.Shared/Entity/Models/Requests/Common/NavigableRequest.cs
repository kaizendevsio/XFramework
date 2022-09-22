namespace XFramework.Client.Shared.Entity.Models.Requests.Common;

public class NavigableRequest
{
    public bool Silent { get; set; }
    public string NavigateToOnSuccess { get; set; }
    public string NavigateToOnFailure { get; set; }
    public string NavigateToOnVerificationRequired { get; set; }
}