using Blazored.LocalStorage;
using Microsoft.Extensions.Configuration;
using XFramework.Client.Shared.Core.Features.Application;
using XFramework.Integration.Interfaces;
using XFramework.Integration.Services.Helpers;
using SessionState = XFramework.Client.Shared.Core.Features.Session.SessionState;

namespace XFramework.Client.Shared.Core.Features.Affiliate;

public partial class AffiliateState
{
    protected class CreateSubscriptionHandler : ActionHandler<CreateSubscription, CmdResponse>
    {
        public IHelperService HelperService { get; }
        public IIdentityServiceWrapper IdentityServiceWrapper { get; }
        private AffiliateState CurrentState => Store.GetState<AffiliateState>();
        
        public CreateSubscriptionHandler(IHelperService helperService, IIdentityServiceWrapper identityServiceWrapper, IConfiguration configuration, ISessionStorageService sessionStorageService,
            ILocalStorageService localStorageService, SweetAlertService sweetAlertService,
            NavigationManager navigationManager, EndPointsModel endPoints, IHttpClient httpClient,
            HttpClient baseHttpClient, IJSRuntime jsRuntime, IMediator mediator, IStore store) : base(configuration,
            sessionStorageService, localStorageService, sweetAlertService, navigationManager, endPoints, httpClient,
            baseHttpClient, jsRuntime, mediator, store)
        {
            HelperService = helperService;
            IdentityServiceWrapper = identityServiceWrapper;
            Configuration = configuration;
            SessionStorageService = sessionStorageService;
            LocalStorageService = localStorageService;
            SweetAlertService = sweetAlertService;
            NavigationManager = navigationManager;
            EndPoints = endPoints;
            HttpClient = httpClient;
            BaseHttpClient = baseHttpClient;
            JsRuntime = jsRuntime;
            Mediator = mediator;
            Store = store;
        }

        public override async Task<CmdResponse> Handle(CreateSubscription action, CancellationToken aCancellationToken)
        {
            await Mediator.Send(new ApplicationState.SetState(){IsBusy = true});
            switch (action.ContactType)
            {
                case GenericContactType.Email:
                    SessionState.RegisterVm.EmailAddress = CurrentState.AffiliateSubscriptionVm.Value;
                    CurrentState.AffiliateSubscriptionVm.SubscriptionEntityGuid = Guid.Parse("0904b14a-260c-41f5-af82-038c9a7f2bef");
                    break;
                case GenericContactType.Phone:
                    CurrentState.AffiliateSubscriptionVm.Value = CurrentState.AffiliateSubscriptionVm.Value.ValidatePhoneNumber();
                    SessionState.RegisterVm.PhoneNumber = CurrentState.AffiliateSubscriptionVm.Value;
                    CurrentState.AffiliateSubscriptionVm.SubscriptionEntityGuid = Guid.Parse("86662fbc-4623-450d-93f1-f11b2913305e");
                    break;
            }

            var response = await IdentityServiceWrapper.CreateAffiliateSubscription(CurrentState.AffiliateSubscriptionVm);
            if (await HandleFailure(response, action))
            {
                return new()
                {
                    IsSuccess = false,
                    Message = response.Message
                };
            }

            await Mediator.Send(new SessionState.InitiateVerificationCode
            {
                LocalVerification = true,
                LocalToken = $"{HelperService.GenerateRandomNumber(111111, 999999)}",
                ContactType = GenericContactType.Phone,
                Contact = CurrentState.AffiliateSubscriptionVm.Value,
                NavigateToOnSuccess = action.NavigateToOnSuccess,
                NavigateToOnFailure = action.NavigateToOnFailure,
                NavigateToOnVerificationRequired = action.NavigateToOnVerificationRequired
            });
            
            await Mediator.Send(new SessionState.SetState() {RegisterVm = SessionState.RegisterVm});
            await Mediator.Send(new ApplicationState.SetState(){IsBusy = false});
            //await HandleSuccess(response, action, true);
            return new()
            {
                IsSuccess = true,
                HttpStatusCode = HttpStatusCode.Accepted,
                Message = response.Message
            };
            
        }
    }
}