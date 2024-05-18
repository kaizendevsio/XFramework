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

            await Mediator.Send(new SubmitVerificationCode()
            {
                OnSuccess = async () =>
                {
                    var result = await identityServerServiceWrapper.ChangePassword(new()
                    {
                        CreadentialId = default,
                        NewPassword = null,
                        VerificationId = default
                    });
                },
            });
            
            /*
            // Handle if the response is invalid or error
            if (await HandleFailure(response, action, true, "There was an error resetting your password"))
            {
                return;
            };
            
            await Mediator.Send(new ApplicationState.SetState(){IsBusy = false});

            // If Success URL property is provided, navigate to the given URL
            await HandleSuccess(response, action, true, "Request password reset request successful");
            return;*/
        }
    }
}

