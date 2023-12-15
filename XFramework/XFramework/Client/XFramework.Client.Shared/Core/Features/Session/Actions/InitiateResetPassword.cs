using XFramework.Client.Shared.Entity.Models.Requests.Common;
using XFramework.Integration.Abstractions;
using XFramework.Integration.Services.Helpers;

namespace XFramework.Client.Shared.Core.Features.Session;

public partial class SessionState
{
    public class InitiateResetPassword : NavigableRequest, IRequest<CmdResponse>;
    
    public class InitiateResetPasswordHandler(IHelperService helperService, HandlerServices handlerServices, IStore store)
        : ActionHandler<InitiateResetPassword, CmdResponse>(handlerServices, store)
    {
        private SessionState CurrentState => Store.GetState<SessionState>();
        
        public override async Task<CmdResponse> Handle(InitiateResetPassword action, CancellationToken aCancellationToken)
        {
            await Mediator.Send(new ApplicationState.SetState() { IsBusy = true });

            try
            {
                CurrentState.ResetPasswordVm.PhoneEmailUsername = CurrentState.ResetPasswordVm.PhoneEmailUsername.ValidatePhoneNumber();
                await Mediator.Send(new InitiateVerificationCode
                {
                    LocalVerification = true,
                    LocalToken = $"{helperService.GenerateRandomNumber(111111, 999999)}",
                    ContactType = GenericContactType.Phone,
                    Contact = SessionState.ResetPasswordVm.PhoneEmailUsername,
                    NavigateToOnSuccess = action.NavigateToOnSuccess,
                    NavigateToOnFailure = action.NavigateToOnFailure,
                    NavigateToOnVerificationRequired = action.NavigateToOnVerificationRequired,
                });
            }
            catch (Exception e)
            {
                SweetAlertService.FireAsync("Error", $"{e.Message}", SweetAlertIcon.Error);
                return new()
                {
                    Message = e.Message,
                    HttpStatusCode = HttpStatusCode.InternalServerError,
                };
            }

            await Mediator.Send(new ApplicationState.SetState() { IsBusy = false });
            return new()
            {
                HttpStatusCode = HttpStatusCode.Accepted,
            };
        }
    }
}