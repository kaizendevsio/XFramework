using MudBlazor;
using XFramework.Administrator.Shared.Modals;

namespace XFramework.Administrator.Pages.Dashboard;

public class EnterprisesBase : PageBase
{
    DialogOptions maxWidth = new DialogOptions() { MaxWidth = MaxWidth.Medium, FullWidth = true };
    
    public void OpenDialog(DialogOptions options)
    {
        DialogService.Show<CreateModal>();
    }
}