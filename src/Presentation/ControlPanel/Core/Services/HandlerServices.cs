using Blazored.LocalStorage;
using Blazored.SessionStorage;
using CurrieTechnologies.Razor.SweetAlert2;
using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using XFramework.Blazor.Core.Helpers;
using XFramework.Blazor.Entity.Models.Components;

namespace ControlPanel.Core.Services;

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
