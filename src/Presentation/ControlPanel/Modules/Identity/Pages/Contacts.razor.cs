using Bogus;
using ControlPanel.Modules.Identity.ViewModels;

namespace ControlPanel.Modules.Identity.Pages;

public partial class Contacts
{
    public List<ContactVm> List { get; set; } = [];
    
    public Contacts()
    {
        View.Title = "Contacts";
    }

    protected override Task OnInitializedAsync()
    {
        var faker = new Faker<ContactVm>()
            .RuleFor(x => x.Type, faker => faker.PickRandom(new[] { "Email", "Phone" }))
            .RuleFor(x => x.Value, (faker, contact) => contact.Type == "Email" ? faker.Internet.Email() : faker.Phone.PhoneNumber())
            .RuleFor(x => x.Verified, faker => faker.Random.Bool())
            .RuleFor(x => x.CreatedAt, faker => faker.Date.Past(1));

        List = faker.Generate(5);
        return base.OnInitializedAsync();
    }

    private void ButtonAction()
    {
        
    }

   

}
