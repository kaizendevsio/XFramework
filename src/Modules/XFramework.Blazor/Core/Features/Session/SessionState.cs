using XFramework.Blazor.Entity.Models.Requests.Session;
using XFramework.Domain.Shared.Contracts;

namespace XFramework.Blazor.Core.Features.Session;

public partial class SessionState : State<SessionState>
{
    public override void Initialize() { }
    public CurrentSessionState State { get; set; }
    public List<IdentityContact>? ContactList { get; set; }
    public IdentityCredential? Credential { get; set; }
    public IdentityInformation? Identity { get; set; }
    public SignInRequest LoginVm { get; set; }= new();
    public SignUpRequest RegisterVm { get; set; }= new();
    public ResetPasswordRequest ResetPasswordVm { get; set; } = new();
    public VerificationRequest VerificationVm { get; set; } = new();
    public List<string> NavigationHistoryList { get; set; } = [];
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public Guid? SessionId { get; set; }
}