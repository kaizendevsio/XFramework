using IdentityServer.Domain.Shared.Contracts.Requests;
using IdentityServer.Integration.Drivers;
using XFramework.Blazor.Entity.Models.Requests.Common;

namespace XFramework.Blazor.Core.Features.Session;

public partial class SessionState
{
    public record ChangePassword : NavigableRequest, IRequest<CmdResponse>;
    
    protected class ChangePasswordHandler(
        IIdentityServerServiceWrapper identityServerServiceWrapper,
        HandlerServices handlerServices,
        IStore store)
        : StateActionHandler<ChangePassword>(handlerServices, store)
    {
        public SessionState CurrentState => Store.GetState<SessionState>();

        public override async Task Handle(ChangePassword action, CancellationToken aCancellationToken)
        {
            ReportBusy();
            
            var result = await identityServerServiceWrapper.ChangePassword(new()
            {
                CreadentialId = SessionState.VerificationVm.CredentialId,
                NewPassword = SessionState.ResetPasswordVm.Password,
                VerificationId = SessionState.VerificationVm.Id
            });
                    
            // Handle if the response is invalid or error
            if (await HandleFailure(result, action, false, "There was an error changing your password"))
            {
                return;
            }
            
            // If Success URL property is provided, navigate to the given URL
            await HandleSuccess(result, action, true, "Password changed successfully");
            return;
        }
    }
}

