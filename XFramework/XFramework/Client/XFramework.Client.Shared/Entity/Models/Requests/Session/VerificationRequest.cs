using XFramework.Client.Shared.Entity.Models.Requests.Common;

namespace XFramework.Client.Shared.Entity.Models.Requests.Session;

public class VerificationRequest : NavigableRequest
{
    public string OtpCode { get; set; }
    public Action OnSuccess { get; set; }
    public Action OnFailure { get; set; }
    public bool? LocalVerification { get; set; }
    public string LocalToken { get; set; }
}