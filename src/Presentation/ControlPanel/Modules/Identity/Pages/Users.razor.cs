using Bogus;
using ControlPanel.Modules.Identity.ViewModels;

namespace ControlPanel.Modules.Identity.Pages;

public partial class Users
{
    public List<UserVm> List { get; set; } = [];
    
    public Users()
    {
        View.Title = "Users";
    }

    protected override Task OnInitializedAsync()
    {
        var faker = new Faker<UserVm>()
            .RuleFor(x => x.UserName, faker => faker.Person.UserName)
            .RuleFor(x => x.Email, faker => faker.Person.Email)
            .RuleFor(x => x.Status, faker => faker.PickRandom(new[] { "Active", "Inactive", "Pending" }))
            .RuleFor(x => x.Role, faker => faker.PickRandom(new[] { "Admin", "User", "Guest" }))
            .RuleFor(x => x.LastLogin, faker => faker.Date.Recent(30))
            .RuleFor(x => x.CreatedAt, faker => faker.Date.Past(2));

        List = faker.Generate(5);
        return base.OnInitializedAsync();
    }

    private void ButtonAction()
    {
        
    }

   

}
