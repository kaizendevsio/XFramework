using XFramework.Administrator.Shared.Modals;

namespace XFramework.Administrator.Pages.Module;

public class EnterprisesBase : PageBase
{
    DialogOptions dialogOptions = new DialogOptions() { MaxWidth = MaxWidth.Small, FullWidth = true };
    
    public void CreateModal(DialogOptions options)
    {
        DialogService.Show<CreateModal>("Create Title",dialogOptions);
    } 
    public void UpdateModal(DialogOptions options)
    {
        DialogService.Show<UpdateModal>(string.Empty,dialogOptions);
    }
}