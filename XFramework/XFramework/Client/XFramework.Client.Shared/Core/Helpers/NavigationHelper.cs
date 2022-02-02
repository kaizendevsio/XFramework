namespace XFramework.Client.Shared.Core.Helpers;

public class NavigationHelper
{
    public IJSRuntime JsRuntime { get; }

    public NavigationHelper(IJSRuntime jsRuntime)
    {
        JsRuntime = jsRuntime;
    }

    public async Task GoBack()
    {
        await JsRuntime.InvokeVoidAsync("App.Navigation.Back");
    }
}