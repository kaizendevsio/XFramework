using XFramework.Blazor.Entity.Models.Requests.Common;

namespace XFramework.Blazor.Entity.Models.Requests.Session;

[SuppressMessage("BlazorState", "TW0001:Blazor State Action should be a nested type of its State")]
public record VerificationRequest : NavigableRequest
{
    public Guid Id { get; set; }
    public Guid CredentialId { get; set; }
    public string OtpCode { get; set; }
    public Action? OnValidToken { get; set; }
    public Action? OnInvalidToken { get; set; }
    public bool? LocalVerification { get; set; }
    public string LocalToken { get; set; }
}