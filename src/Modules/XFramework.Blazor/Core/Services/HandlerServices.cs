namespace XFramework.Blazor.Core.Services;

public record HandlerServices(
    IConfiguration Configuration,
    ISessionStorageService SessionStorageService,
    IHostEnvironment HostEnvironment,
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
