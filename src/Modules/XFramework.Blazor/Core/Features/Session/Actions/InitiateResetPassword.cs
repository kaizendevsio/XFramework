using IdentityServer.Integration.Drivers;
using XFramework.Blazor.Entity.Models.Requests.Common;
using XFramework.Domain.Shared.Contracts;
using XFramework.Integration.Abstractions;
using XFramework.Integration.Services.Helpers;

namespace XFramework.Blazor.Core.Features.Session;

public partial class SessionState
{
    public record InitiateResetPassword : NavigableRequest;
    
    public class InitiateResetPasswordHandler(
        IHelperService helperService,
        IIdentityServerServiceWrapper identityServerServiceWrapper,
        HandlerServices handlerServices, 
        IStore store)
        : StateActionHandler<InitiateResetPassword>(handlerServices, store)
    {
        private SessionState CurrentState => Store.GetState<SessionState>();
        
        public override async Task Handle(InitiateResetPassword action, CancellationToken aCancellationToken)
        {
            await Mediator.Send(new ApplicationState.SetState() { IsBusy = true });

            try
            {
                CurrentState.ResetPasswordVm.PhoneEmailUsername = CurrentState.ResetPasswordVm.PhoneEmailUsername.ValidatePhoneNumber();
                
                // check if the phone number exists
                var user = await identityServerServiceWrapper.IdentityContact.GetList(
                    pageNumber: 1,
                    pageSize: 2,
                    filter:
                    [
                        new()
                        {
                            PropertyName = nameof(IdentityContact.Value),
                            Operation = QueryFilterOperation.Equal,
                            Value = CurrentState.ResetPasswordVm.PhoneEmailUsername,
                        }
                    ]);

                if (user.IsSuccess is false)
                {
                    await SweetAlertService.FireAsync("Error", $"{user.Message}", SweetAlertIcon.Error);
                    return;
                }

                if (user.Response.TotalItems == 0)
                {
                    await SweetAlertService.FireAsync("Error", "The phone number does not exist", SweetAlertIcon.Error);
                    return;
                }
                
                await Mediator.Send(new InitiateVerificationCode
                {
                    CredentialId = user.Response.Items.First().CredentialId,
                    NavigateToOnSuccess = action.NavigateToOnSuccess,
                    NavigateToOnFailure = action.NavigateToOnFailure,
                    NavigateToOnVerificationRequired = action.NavigateToOnVerificationRequired,
                });
            }
            catch (Exception e)
            {
                SweetAlertService.FireAsync("Error", $"{e.Message}", SweetAlertIcon.Error);
            }

            await Mediator.Send(new ApplicationState.SetState() { IsBusy = false });
        }
    }
}