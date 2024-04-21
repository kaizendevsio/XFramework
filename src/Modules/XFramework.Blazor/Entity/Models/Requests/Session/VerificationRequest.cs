using XFramework.Blazor.Entity.Models.Requests.Common;

namespace XFramework.Blazor.Entity.Models.Requests.Session;

[SuppressMessage("BlazorState", "TW0001:Blazor State Action should be a nested type of its State")]
public record VerificationRequest : NavigableRequest
{
    public string OtpCode { get; set; }
    public Action OnSuccess { get; set; }
    public Action OnFailure { get; set; }
    public bool? LocalVerification { get; set; }
    public string LocalToken { get; set; }
}