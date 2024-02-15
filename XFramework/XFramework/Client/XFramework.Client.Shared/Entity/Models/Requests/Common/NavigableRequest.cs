using XFramework.Client.Shared.Core.Features;

namespace XFramework.Client.Shared.Entity.Models.Requests.Common;

public record NavigableRequest : StateAction
{
    public bool Silent { get; set; }
    public string? NavigateToOnSuccess { get; set; }
    public string? NavigateToOnFailure { get; set; }
    public string? NavigateToOnVerificationRequired { get; set; }
}