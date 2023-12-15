using IdentityServer.Integration.Drivers;
using XFramework.Client.Shared.Entity.Models.Requests.Common;

namespace XFramework.Client.Shared.Core.Features.Session;

public partial class SessionState
{
    public class SendVerificationCode : NavigableRequest, IRequest<CmdResponse>;
    
    public class SendVerificationCodeHandler(
        IIdentityServerServiceWrapper identityServerServiceWrapper,
        HandlerServices handlerServices,
        IStore store)
        : ActionHandler<SendVerificationCode, CmdResponse>(handlerServices, store)
    {
        public SessionState CurrentState => Store.GetState<SessionState>();

        public override async Task<CmdResponse> Handle(SendVerificationCode action, CancellationToken aCancellationToken)
        {
            action.NavigateToOnSuccess = CurrentState.VerificationVm.NavigateToOnSuccess;
            action.NavigateToOnFailure = CurrentState.VerificationVm.NavigateToOnFailure;

            if (CurrentState.VerificationVm.LocalVerification is true)
            {
                if (SessionState.VerificationVm.LocalToken != SessionState.VerificationVm.OtpCode)
                {
                    await SweetAlertService.FireAsync(new SweetAlertOptions
                    {
                        Title = "Error",
                        Text = "Your otp code is incorrect. Please try again",
                        Icon = SweetAlertIcon.Error,
                        ShowCloseButton = true,
                        ConfirmButtonText = "Close",
                    });
                    CurrentState.VerificationVm.OnFailure?.Invoke();
                    return new()
                    {
                        HttpStatusCode = HttpStatusCode.BadRequest,
                        
                    };
                }
                await NavigateTo(action.NavigateToOnSuccess);
            }
            else
            {
                var response = await identityServerServiceWrapper.IdentityVerification.Patch(new()
                {
                    Token = CurrentState.VerificationVm.OtpCode,
                });
                
                if (await HandleFailure(response, action, true, "Your otp code is incorrect. Please try again"))
                {
                    CurrentState.VerificationVm.OnFailure?.Invoke();
                    return new()
                    {
                        HttpStatusCode = HttpStatusCode.BadRequest
                    };
                }
                await HandleSuccess(response, action, true);
            }

            CurrentState.VerificationVm.OnSuccess?.Invoke();
            CurrentState.VerificationVm = new();
            
            return new()
            {
                HttpStatusCode = HttpStatusCode.Accepted,
                
            };
        }
    }
}