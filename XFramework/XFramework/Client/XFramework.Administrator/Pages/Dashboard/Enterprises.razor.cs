using MudBlazor;

namespace XFramework.Administrator.Pages.Dashboard;

public class EnterprisesBase : PageBase
{
    DialogOptions maxWidth = new DialogOptions() { MaxWidth = MaxWidth.Medium, FullWidth = true };
    
    private void OpenDialog(DialogOptions options)
    {
        Dialog.Show<DialogUsageExample_Dialog>("Custom Options Dialog", options);
    }
}