using XFramework.Administrator.Shared.Modals;

namespace XFramework.Administrator.Pages.Dashboard;

public class EnterprisesBase : PageBase
{
    DialogOptions maxWidth = new DialogOptions() { MaxWidth = MaxWidth.Medium, FullWidth = true };
    
    public void CreateModal(DialogOptions options)
    {
        DialogService.Show<CreateModal>();
    } 
    public void UpdateModal(DialogOptions options)
    {
        DialogService.Show<UpdateModal>();
    }
}