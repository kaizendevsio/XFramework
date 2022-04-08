namespace XFramework.Administrator.Pages;

public class IndexBase : PageBase
{
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            NavigationManager.NavigateTo("Session/SignIn");
        }
        await base.OnAfterRenderAsync(firstRender);
    }
}