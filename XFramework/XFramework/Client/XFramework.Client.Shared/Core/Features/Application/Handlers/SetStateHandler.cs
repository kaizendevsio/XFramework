namespace XFramework.Client.Shared.Core.Features.Application;

public partial class ApplicationState
{
    protected class SetStateHandler : ActionHandler<SetState>
    {
        private ApplicationState CurrentState => Store.GetState<ApplicationState>();
            
        public SetStateHandler(IndexedDbService indexedDbService, IConfiguration configuration, ISessionStorageService sessionStorageService, ILocalStorageService localStorageService, SweetAlertService sweetAlertService, NavigationManager navigationManager, EndPointsModel endPoints, IHttpClient httpClient, HttpClient baseHttpClient, IJSRuntime jsRuntime, IMediator mediator, IStore store) : base(configuration, sessionStorageService, localStorageService, sweetAlertService, navigationManager, endPoints, httpClient, baseHttpClient, jsRuntime, mediator, store)
        {
            IndexedDbService = indexedDbService;
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
                await HandleProgressStatus(action);
                Persist(CurrentState);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            return Unit.Value;
        }

        private async Task HandleProgressStatus(SetState action)
        {
            if (!string.IsNullOrEmpty(action.ProgressTitle))
            {
                SweetAlertService.UpdateAsync(new()
                {
                    Title = action.ProgressTitle,
                    Html = $"<div class='loadingio-spinner-ellipsis-hm5jphe6my'><div class='ldio-o8ctnog1lcq'><div></div><div></div><div></div><div></div><div></div></div></div>",
                });
            }
            if (!string.IsNullOrEmpty(action.ProgressMessage))
            {
                SweetAlertService.UpdateAsync(new()
                {
                    //Title = CurrentState.ProgressTitle,
                    Html = $"<div class='loadingio-spinner-ellipsis-hm5jphe6my'><div class='ldio-o8ctnog1lcq'><div></div><div></div><div></div><div></div><div></div></div></div> <p>{action.ProgressMessage}</p>",
                });
            }
            
            switch (action.IsBusy)
            {
                case null:
                    return;
                case true:
                    if (action.NoSpinner is false or null)
                    {
                        SweetAlertService.FireAsync(new()
                        {
                            //Title = CurrentState.ProgressTitle,
                            Text = CurrentState.ProgressMessage,
                            Backdrop = false,
                            Html = $"<div class='loadingio-spinner-ellipsis-hm5jphe6my'><div class='ldio-o8ctnog1lcq'><div></div><div></div><div></div><div></div><div></div></div></div>",
                            ShowConfirmButton = false,
                        });
                    }
                    break;
                case false:
                    SweetAlertService.CloseAsync();
                    break;
            }
        }
    }
}