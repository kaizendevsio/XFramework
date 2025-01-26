using Bogus;
using ControlPanel.Modules.Identity.ViewModels;

namespace ControlPanel.Modules.Storage.Pages;

public partial class Storage
{
    public List<StorageVm> List { get; set; } = [];
    
    public Storage()
    {
        View.Title = "Storage";
    }

    protected override Task OnInitializedAsync()
    {
        var faker = new Faker<StorageVm>()
            .RuleFor(x => x.ItemName, faker => faker.Commerce.ProductName())
            .RuleFor(x => x.Quantity, faker => faker.Random.Int(1, 1000))
            .RuleFor(x => x.Location, faker => faker.Address.City())
            .RuleFor(x => x.Status, faker => faker.PickRandom(new[] { "In Stock", "Out of Stock", "Reserved" }))
            .RuleFor(x => x.LastUpdated, faker => faker.Date.Recent(30));

        List = faker.Generate(5);
        return base.OnInitializedAsync();
    }

    private void ButtonAction()
    {
        
    }

   

}
