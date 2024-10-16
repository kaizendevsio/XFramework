﻿using XFramework.Blazor.Core.Features.Address;
using XFramework.Blazor.Core.Features.Cache;
using XFramework.Blazor.Core.Features.Identity;
using XFramework.Blazor.Core.Features.Layout;
using XFramework.Blazor.Core.Features.Wallet;

namespace XFramework.Blazor.Components;

public class XLayoutBase : BlazorStateLayoutComponent
{
    // Inject Services
    [Inject] public HttpClient BaseHttpClient { get; set; }
    [Inject] public IHttpClient HttpClient { get; set; }
    [Inject] public NavigationManager NavigationManager { get; set; }
    [Inject] public ISessionStorageService SessionStorageService { get; set; }
    [Inject] public SweetAlertService SweetAlertService { get; set; }
    [Inject] public IJSRuntime JsRuntime { get; set; }
    [Inject] public EndPointsModel EndPoints { get; set; }
    [Inject] public IDialogService DialogService { get; set; }
    [Inject] public IMediator Mediator { get; set; }
    
    
    public ApplicationState ApplicationState => GetState<ApplicationState>();
    public LayoutState LayoutState => GetState<LayoutState>();
    public SessionState SessionState => GetState<SessionState>();
    public IdentityState IdentityState => GetState<IdentityState>();
    public AddressState AddressState => Store.GetState<AddressState>();
    public CacheState CacheState => GetState<CacheState>();
    public WalletState WalletState => GetState<WalletState>();

    public void NavigateTo(string path) => NavigationManager.NavigateTo(path);
    public async Task NavigateBack() => await JsRuntime.InvokeVoidAsync("history.back");
}