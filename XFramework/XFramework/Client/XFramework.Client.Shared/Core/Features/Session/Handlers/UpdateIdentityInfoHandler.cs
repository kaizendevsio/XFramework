using IdentityServer.Domain.Generic.Contracts.Requests.Update;

namespace XFramework.Client.Shared.Core.Features.Session;

public partial class SessionState
{
    protected class UpdateIdentityInfoHandler : ActionHandler<UpdateIdentityInfo, CmdResponse>
    {
        public IIdentityServiceWrapper IdentityServiceWrapper { get; }
        public SessionState CurrentState => Store.GetState<SessionState>();
        
        public UpdateIdentityInfoHandler(IIdentityServiceWrapper identityServiceWrapper, IndexedDbService indexedDbService, IConfiguration configuration, ISessionStorageService sessionStorageService, ILocalStorageService localStorageService, SweetAlertService sweetAlertService, NavigationManager navigationManager, EndPointsModel endPoints, IHttpClient httpClient, HttpClient baseHttpClient, IJSRuntime jsRuntime, IMediator mediator, IStore store) : base(configuration, sessionStorageService, localStorageService, sweetAlertService, navigationManager, endPoints, httpClient, baseHttpClient, jsRuntime, mediator, store)
        {
            IdentityServiceWrapper = identityServiceWrapper;
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

        public override async Task<CmdResponse> Handle(UpdateIdentityInfo action, CancellationToken aCancellationToken)
        {
            ReportTask("Processing Request..");
            var response = await IdentityServiceWrapper.UpdateIdentity(action.IdentityInfo.Adapt<UpdateIdentityRequest>());
            ReportTaskCompleted(action);

            if (await HandleFailure(response, action, true)) return response.Adapt<CmdResponse>();
            await HandleSuccess(response, action, false ,"Updated successfully");
            
            return new()
            {
                
                Message = "Success",
                HttpStatusCode = HttpStatusCode.Accepted,
            };
        }
    }
}