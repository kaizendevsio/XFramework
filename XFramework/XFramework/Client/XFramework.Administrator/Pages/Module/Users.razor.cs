using Bogus;
using XFramework.Administrator.Shared.Modals;
using XFramework.Client.Shared.Entity.Models;

namespace XFramework.Administrator.Pages.Module;

public class UsersBase : PageBase
{
  public UsersBase()
  {
    View.Title = "Users";
  }
  
  protected override async Task OnAfterRenderAsync(bool firstRender)
  {
    if (firstRender)
    {
      //await GenerateModelFaker();
    }
    await base.OnAfterRenderAsync(firstRender);
  }

  DialogOptions dialogOptions = new DialogOptions() { MaxWidth = MaxWidth.Small, FullWidth = true };
    
  public void CreateModal(DialogOptions options)
  {
    DialogService.Show<CreateModal>("Create Title",dialogOptions);
  } 
  
  
}