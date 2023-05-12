using Bogus;
using XFramework.Administrator.Shared.Modals;
using XFramework.Client.Shared.Entity.Models;

namespace XFramework.Administrator.Pages.Module;

public class EnterprisesBase : PageBase
{
    DialogOptions dialogOptions = new DialogOptions() { MaxWidth = MaxWidth.Small, FullWidth = true };
    
    public void CreateModal(DialogOptions options)
    {
        DialogService.Show<CreateModal>("Create Title",dialogOptions);
    } 
    
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            //await GenerateModelFaker();
        }
        await base.OnAfterRenderAsync(firstRender);
    }
   
    
    public void EditModal(DialogOptions options)
    {
        DialogService.Show<EditModal>("Edit",dialogOptions);
    }
    public void DeleteModal(DialogOptions options)
    {
        DialogService.Show<DeleteModal>("Delete",dialogOptions);
    } 

    public EventCallback<string> Edit { get; set; }
    public async Task EditValue()
    {
        /*await Edit.InvokeAsync()*/
    }
    

}