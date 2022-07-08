using Blazored.LocalStorage;
using Microsoft.Extensions.Configuration;

namespace XFramework.Client.Shared.Core.Features.Todo;

public partial class TodoState
{
    protected class CreateTodoHandler : ActionHandler<CreateTodo>
    {
        public CreateTodoHandler(IConfiguration configuration, ISessionStorageService sessionStorageService, ILocalStorageService localStorageService, SweetAlertService sweetAlertService, NavigationManager navigationManager, EndPointsModel endPoints, IHttpClient httpClient, HttpClient baseHttpClient, IJSRuntime jsRuntime, IMediator mediator, IStore store) : base(configuration, sessionStorageService, localStorageService, sweetAlertService, navigationManager, endPoints, httpClient, baseHttpClient, jsRuntime, mediator, store)
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

        public override async Task<Unit> Handle(CreateTodo action, CancellationToken aCancellationToken)
        {
            /*
            if (!action.Silent)
            {
                ReportTask("Creating Wallet..", true);
            }
            
            if (!action.Silent)
            {
                ReportTask("Done", false);
            }
            */
            Console.WriteLine("Hello world");
            TodoState.Text = "Hello world";
            
            return Unit.Value;
        }
    }
}
