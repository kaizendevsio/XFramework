using IdentityServer.Domain.Shared.Contracts.Requests;
using IdentityServer.Integration.Drivers;
using XFramework.Blazor.Entity.Models.Requests.Common;

namespace XFramework.Blazor.Core.Features.Session;

public partial class SessionState
{
    public record ResetPassword : NavigableRequest, IRequest<CmdResponse>
    {
        public string? PhoneEmailUsername { get; set; }
    }
    
    protected class ResetPasswordHandler(
        IIdentityServerServiceWrapper identityServerServiceWrapper,
        HandlerServices handlerServices,
        IStore store)
        : StateActionHandler<ResetPassword>(handlerServices, store)
    {
        public SessionState CurrentState => Store.GetState<SessionState>();

        public override async Task Handle(ResetPassword action, CancellationToken aCancellationToken)
        {
            await Mediator.Send(new ApplicationState.SetState(){IsBusy = true});

            // Map view model to request object
            var request = new ResetPasswordRequest
            {
                PhoneEmailUsername = CurrentState.ResetPasswordVm.PhoneEmailUsername
            };
            
            // Send the request
            var response = await identityServerServiceWrapper.ResetPassword(request);
            CurrentState.ResetPasswordVm = new();
            
            // Handle if the response is invalid or error
            if (await HandleFailure(response, action, true, "There was an error resetting your password"))
            {
                return;
            };
            
            await Mediator.Send(new ApplicationState.SetState(){IsBusy = false});

            // If Success URL property is provided, navigate to the given URL
            await HandleSuccess(response, action, false, "Password reset request successful");
            return;
        }
    }
}

