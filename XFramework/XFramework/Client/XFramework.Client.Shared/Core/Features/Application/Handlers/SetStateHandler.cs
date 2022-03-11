using Blazored.LocalStorage;
using Microsoft.Extensions.Configuration;

namespace XFramework.Client.Shared.Core.Features.Application;

public partial class ApplicationState
{
    public class SetStateHandler : ActionHandler<SetState>
    {
        private ApplicationState CurrentState => Store.GetState<ApplicationState>();
            
        public SetStateHandler(IConfiguration configuration, ISessionStorageService sessionStorageService, ILocalStorageService localStorageService, SweetAlertService sweetAlertService, NavigationManager navigationManager, EndPointsModel endPoints, IHttpClient httpClient, HttpClient baseHttpClient, IJSRuntime jsRuntime, IMediator mediator, IStore store) : base(configuration, sessionStorageService, localStorageService, sweetAlertService, navigationManager, endPoints, httpClient, baseHttpClient, jsRuntime, mediator, store)
        {
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

        public override async Task<Unit> Handle(SetState action, CancellationToken aCancellationToken)
        {
            try
            {
                StateHelper.SetProperties(action,CurrentState);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            HandleProgressStatus();
            return Unit.Value;
        }

        private async Task HandleProgressStatus()
        {
            if (CurrentState.IsBusy)
            {
                SweetAlertService.FireAsync(new()
                {
                    Title = CurrentState.ProgressTitle,
                    Text = CurrentState.ProgressMessage,
                    Html = "<img src='assets/img/loader.svg' width='96px' />",
                    ShowConfirmButton = false,
                });
            }
            else
            {
                SweetAlertService.CloseAsync();
            }
        }
    }
}