using Microsoft.AspNetCore.Components.Forms;
using MudBlazor;

namespace XFramework.Administrator.Pages.Dashboard;

public class SignInBase : PageBase
{
    
    bool isShow;
    InputType PasswordInput = InputType.Password;
    string PasswordInputIcon = Icons.Material.Filled.VisibilityOff;
    public void ShowPassword()
    {
        if (isShow)
        {
            isShow = false;
            PasswordInputIcon = Icons.Material.Filled.VisibilityOff;
            PasswordInput = InputType.Password;
        }
        else {
            isShow = true;
            PasswordInputIcon = Icons.Material.Filled.Visibility;
            PasswordInput = InputType.Text;
        }
    }
    
    public void OnValidSubmit(EditContext context)
    {
        StateHasChanged();
        NavigationManager.NavigateTo("/SignIn");
    }
}