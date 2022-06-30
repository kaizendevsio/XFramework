using Bogus;
using XFramework.Administrator.Shared.Modals;
using XFramework.Client.Shared.Entity.Models;

namespace XFramework.Administrator.Pages.Module;

public class UsersBase : PageBase
{
  public UsersBase()
  {
    View.Title = "HEHEHE";
  }
  
  protected override async Task OnAfterRenderAsync(bool firstRender)
  {
    if (firstRender)
    {
      await GenerateModelFaker();
    }
    await base.OnAfterRenderAsync(firstRender);
  }
  public async Task GenerateModelFaker()
  {
    var model = new Faker<SampleModel>()
        .RuleFor(x => x.Name, x => x.Person.FullName)
        .RuleFor(x => x.Email, x => x.Person.Email)
        .RuleFor(x => x.Phone, x => x.Person.Phone)
        .RuleFor(x => x.DateOfBirth, x => x.Person.DateOfBirth)
        .RuleFor(x => x.Status, x => x.Random.Int(0, 3))
      ;

    Model = model.GenerateBetween(1, 25);
    StateHasChanged();
  }
  
  DialogOptions dialogOptions = new DialogOptions() { MaxWidth = MaxWidth.Small, FullWidth = true };
    
  public void CreateModal(DialogOptions options)
  {
    DialogService.Show<CreateModal>("Create Title",dialogOptions);
  } 
  
  
}