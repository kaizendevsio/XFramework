using IdentityServer.Domain.Generic.Contracts.Responses;
using XFramework.Client.Shared.Entity.Models.Requests.Session;
using XFramework.Domain.Generic.Contracts;

namespace XFramework.Client.Shared.Core.Features.Session;

public partial class SessionState : State<SessionState>
{
    public override void Initialize()
    {
         NavigationHistoryList = new();
         LoginVm = new();
         RegisterVm = new();
         ResetPasswordVm = new();
         VerificationVm = new();
    }
    public CurrentSessionState State { get; set; }
    public List<IdentityContact> ContactList { get; set; }
    public IdentityCredential Credential { get; set; }
    public IdentityInformation Identity { get; set; }
    public SignInRequest LoginVm { get; set; }
    public SignUpRequest RegisterVm { get; set; }
    public ResetPasswordRequest ResetPasswordVm { get; set; }
    public VerificationRequest VerificationVm { get; set; }
    public List<string> NavigationHistoryList { get; set; }

}