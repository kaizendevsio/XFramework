using Bogus;
using ControlPanel.Modules.Identity.ViewModels;

namespace ControlPanel.Modules.Identity.Pages;

public partial class Addresses
{
    public List<AddressVm> List { get; set; } = [];
    
    public Addresses()
    {
        View.Title = "Addresses";
    }

    protected override Task OnInitializedAsync()
    {
        var faker = new Faker<AddressVm>()
            .RuleFor(x => x.Name, faker => faker.PickRandom(new[] { "Home", "Work", "Billing", "Shipping" }))
            .RuleFor(x => x.Description, faker => faker.Lorem.Sentence());

        List = faker.Generate(5);
        return base.OnInitializedAsync();
    }

    private void ButtonAction()
    {
        
    }

   

}
