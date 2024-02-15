namespace XFramework.Client.Shared.Core.Services;

public record HandlerServices(
    IConfiguration Configuration,
    ISessionStorageService SessionStorageService,
    IWebAssemblyHostEnvironment HostEnvironment,
    ILocalStorageService LocalStorageService,
    SweetAlertService SweetAlertService,
    NavigationManager NavigationManager,
    EndPointsModel EndPoints,
    IHttpClient HttpClient,
    HttpClient BaseHttpClient,
    IJSRuntime JsRuntime,
    IMediator Mediator,
    ISnackbar Snackbar
    );
