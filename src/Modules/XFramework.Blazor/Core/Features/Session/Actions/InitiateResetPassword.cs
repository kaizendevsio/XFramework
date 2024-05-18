﻿using XFramework.Blazor.Entity.Models.Requests.Common;
using XFramework.Integration.Abstractions;
using XFramework.Integration.Services.Helpers;

namespace XFramework.Blazor.Core.Features.Session;

public partial class SessionState
{
    public record InitiateResetPassword : NavigableRequest;
    
    public class InitiateResetPasswordHandler(IHelperService helperService, HandlerServices handlerServices, IStore store)
        : StateActionHandler<InitiateResetPassword>(handlerServices, store)
    {
        private SessionState CurrentState => Store.GetState<SessionState>();
        
        public override async Task Handle(InitiateResetPassword action, CancellationToken aCancellationToken)
        {
            await Mediator.Send(new ApplicationState.SetState() { IsBusy = true });

            try
            {
                CurrentState.ResetPasswordVm.PhoneEmailUsername = CurrentState.ResetPasswordVm.PhoneEmailUsername.ValidatePhoneNumber();
                await Mediator.Send(new InitiateVerificationCode
                {
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