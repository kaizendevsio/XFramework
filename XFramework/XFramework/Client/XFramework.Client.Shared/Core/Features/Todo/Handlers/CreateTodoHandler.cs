using Blazored.LocalStorage;
using Microsoft.Extensions.Configuration;

namespace XFramework.Client.Shared.Core.Features.Todo.Handlers;

public partial class TodoState
{
    protected class CreateTodoHandler : ActionHandler<Actions.TodoState.CreateTodo>
    {
        public CreateTodoHandler(IConfiguration configuration, ISessionStorageService sessionStorageService, ILocalStorageService localStorageService, SweetAlertService sweetAlertService, NavigationManager navigationManager, EndPointsModel endPoints, IHttpClient httpClient, HttpClient baseHttpClient, IJSRuntime jsRuntime, IMediator mediator, IStore store) : base(configuration, sessionStorageService, localStorageService, sweetAlertService, navigationManager, endPoints, httpClient, baseHttpClient, jsRuntime, mediator, store)
        {
        }

        public override async Task<Unit> Handle(Actions.TodoState.CreateTodo action, CancellationToken aCancellationToken)
        {
            if (!action.Silent)
            {
                ReportTask("Creating Wallet..", true);
            }
            
            if (!action.Silent)
            {
                ReportTask("Done", false);
            }

            return Unit.Value;
        }
    }
}
