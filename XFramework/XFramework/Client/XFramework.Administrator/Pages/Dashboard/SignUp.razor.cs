using Microsoft.AspNetCore.Components.Forms;

namespace XFramework.Administrator.Pages.Dashboard;

public class SignUpBase : PageBase
{
    public bool success;

    public void OnValidSubmit(EditContext context)
    {
        success = true;
        StateHasChanged();
        NavigationManager.NavigateTo("/SignIn");
    }
    
    
}