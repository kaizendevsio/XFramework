using IdentityServer.Integration.Drivers;
using XFramework.Blazor.Entity.Models.Requests.Common;
using XFramework.Integration.Abstractions;
using XFramework.Integration.Services.Helpers;

namespace XFramework.Blazor.Core.Features.Affiliate;

public partial class AffiliateState
{
    public record CreateSubscription : NavigableRequest, IRequest<CmdResponse>
    {
        public GenericContactType ContactType { get; set; }
    }
    
    protected class CreateSubscriptionHandler (HandlerServices handlerServices, IStore store, IIdentityServerServiceWrapper identityServerServiceWrapper, IHelperService helperService)
        : StateActionHandler<CreateSubscription>(handlerServices, store)
    {
        private AffiliateState CurrentState => Store.GetState<AffiliateState>();
        
        public override async Task<CmdResponse> Handle(CreateSubscription action, CancellationToken aCancellationToken)
        {
            await Mediator.Send(new ApplicationState.SetState(){IsBusy = true});

            try
            {
                switch (action.ContactType)
                {
                    case GenericContactType.Email:
                        SessionState.RegisterVm.EmailAddress = CurrentState.AffiliateSubscriptionVm.Value;
                        CurrentState.AffiliateSubscriptionVm.SubscriptionEntityId = Guid.Parse("0904b14a-260c-41f5-af82-038c9a7f2bef");
                        break;
                    case GenericContactType.Phone:
                        CurrentState.AffiliateSubscriptionVm.Value = CurrentState.AffiliateSubscriptionVm.Value.ValidatePhoneNumber();
                        SessionState.RegisterVm.PhoneNumber = CurrentState.AffiliateSubscriptionVm.Value;
                        CurrentState.AffiliateSubscriptionVm.SubscriptionEntityId = Guid.Parse("86662fbc-4623-450d-93f1-f11b2913305e");
                        break;
                }
            }
            catch (Exception e)
            {
                SweetAlertService.FireAsync("Error", $"{e.Message}", SweetAlertIcon.Error);
                return new()
                {
                    Message = e.Message
                };
            }

            /*var response = await identityServerServiceWrapper.CreateAffiliateSubscription(CurrentState.AffiliateSubscriptionVm);
            if (await HandleFailure(response, action))
            {
                return new()
                {
                    Message = response.Message
                };
            }*/

            await NavigateTo(action.NavigateToOnSuccess);
            /*await Mediator.Send(new SessionState.InitiateVerificationCode
            {
                LocalVerification = true,
                LocalToken = $"{helperService.GenerateRandomNumber(111111, 999999)}",
                ContactType = GenericContactType.Phone,
                Contact = CurrentState.AffiliateSubscriptionVm.Value,
                NavigateToOnSuccess = action.NavigateToOnSuccess,
                NavigateToOnFailure = action.NavigateToOnFailure,
                NavigateToOnVerificationRequired = action.NavigateToOnVerificationRequired
            });*/
            
            await Mediator.Send(new SessionState.SetState() {RegisterVm = SessionState.RegisterVm});
            await Mediator.Send(new ApplicationState.SetState(){IsBusy = false});
            
            //await HandleSuccess(response, action, true);
            return new()
            {
                HttpStatusCode = HttpStatusCode.Accepted,
                //Message = response.Message
            };
            
        }
    }
}