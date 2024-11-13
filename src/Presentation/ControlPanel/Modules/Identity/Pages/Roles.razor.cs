using Bogus;
using ControlPanel.Modules.Identity.ViewModels;

namespace ControlPanel.Modules.Identity.Pages;

public partial class Roles
{
    public List<RoleVm> List { get; set; } = [];
    
    public Roles()
    {
        View.Title = "Roles";
    }

    protected override Task OnInitializedAsync()
    {
        var faker = new Faker<RoleVm>()
            .RuleFor(x => x.Name, faker => faker.PickRandom(new[] { "Administrator", "Customer", "Moderator", "Guest", "User" }))
            .RuleFor(x => x.Description, faker => faker.Lorem.Sentence())
            .RuleFor(x => x.Permissions, faker => string.Join(", ", faker.PickRandom(new[] { "Read", "Write", "Execute", "Delete" }, faker.Random.Int(1, 3))))
            .RuleFor(x => x.NumberOfUsers, faker => faker.Random.Int(1, 1000))
            .RuleFor(x => x.CreatedAt, faker => faker.Date.Past(2));

        List = faker.Generate(5);
        return base.OnInitializedAsync();
    }

    private void ButtonAction()
    {
        
    }

   

}
