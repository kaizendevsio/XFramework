using Humanizer;
using XFramework.Blazor.Core.Features.Address;
using XFramework.Blazor.Core.Features.Affiliate;
using XFramework.Blazor.Core.Features.Cache;
using XFramework.Blazor.Core.Features.Identity;
using XFramework.Blazor.Core.Features.Layout;
using XFramework.Blazor.Core.Features.Wallet;
using XFramework.Domain.Shared.Contracts;
using XFramework.Integration.Abstractions.Wrappers;

namespace XFramework.Blazor.Components;

public class XComponentsBase : BlazorStateComponent
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
    [Inject] public IMessageBusWrapper MessageBusWrapper { get; set; }
    
    // Initialize States
    public ApplicationState ApplicationState => GetState<ApplicationState>();
    public LayoutState LayoutState => GetState<LayoutState>();
    public SessionState SessionState => GetState<SessionState>();
    public IdentityState IdentityState => GetState<IdentityState>();
    public AffiliateState AffiliateState => GetState<AffiliateState>();
    public AddressState AddressState => GetState<AddressState>();
    public CacheState CacheState => GetState<CacheState>();
    public WalletState WalletState => GetState<WalletState>();

    [Parameter] public bool? IsLoading { get; set; }
    public string Cursor => IsLoading is true ? "progress" : "arrow";
    
    // Global Methods
    public void NavigateTo(string path) => NavigationManager.NavigateTo(path);
    public async Task NavigateBack() => await JsRuntime.InvokeVoidAsync("history.back");

    // Global Methods
    
    public string FullName(IdentityCredential? item) => $"{item?.IdentityInfo.FirstName?.ToLowerInvariant().Humanize(LetterCasing.Title)} {item?.IdentityInfo.MiddleName?.ToLowerInvariant().Humanize(LetterCasing.Title)} {item?.IdentityInfo.LastName?.ToLowerInvariant().Humanize(LetterCasing.Title)}";
    public DateOnly? DateOfBirth(IdentityCredential? item) => item?.IdentityInfo.BirthDate;
    public string NickName(IdentityCredential? item) => $"{item?.IdentityInfo.FirstName?.ToLowerInvariant().Humanize(LetterCasing.Title)} {item?.IdentityInfo.MiddleName?.ToLowerInvariant().Humanize(LetterCasing.Title)} {item?.IdentityInfo.LastName?.ToLowerInvariant().Humanize(LetterCasing.Title)}";
    public string PhoneNumber(IdentityCredential? item) => $"{item?.IdentityContacts.FirstOrDefault(i => i.Type.Name == "Phone")?.Value}";
    public string EmailAddress(IdentityCredential? item) => $"{item?.IdentityContacts.FirstOrDefault(i => i.Type.Name == "Email")?.Value}";

    public void ShowPreloader()
    {
        IsLoading = true;
        InvokeAsync(StateHasChanged);
    }
    
    public void HidePreloader()
    {
        IsLoading = false;
        InvokeAsync(StateHasChanged);
    }
    
}