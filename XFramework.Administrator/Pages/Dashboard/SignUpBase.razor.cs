using Microsoft.AspNetCore.Components.Forms;
using XFramework.Administrator.Shared.Models;

namespace XFramework.Administrator.Pages.Dashboard;

public class SignUpBase : PageBase
{
    public SignUpModel model = new SignUpModel();
    public bool success;

    public void OnValidSubmit(EditContext context)
    {
        success = true;
        StateHasChanged();
        NavigationManager.NavigateTo("/SignIn");
    }
    
    
}