using XFramework.Blazor.Core.Features;

namespace XFramework.Blazor.Entity.Models.Requests.Common;

[SuppressMessage("BlazorState", "TW0001:Blazor State Action should be a nested type of its State")]
public record NavigableRequest : StateAction
{
    public bool Silent { get; set; }
    public string? NavigateToOnSuccess { get; set; }
    public string? NavigateToOnFailure { get; set; }
    public string? NavigateToOnVerificationRequired { get; set; }
}

[SuppressMessage("BlazorState", "TW0001:Blazor State Action should be a nested type of its State")]
public record NavigableRequest<T> : StateAction<T>
{
    public bool Silent { get; set; }
    public string? NavigateToOnSuccess { get; set; }
    public string? NavigateToOnFailure { get; set; }
    public string? NavigateToOnVerificationRequired { get; set; }
}