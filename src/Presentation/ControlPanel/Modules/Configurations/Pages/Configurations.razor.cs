using Bogus;
using ControlPanel.Modules.Identity.ViewModels;

namespace ControlPanel.Modules.Configurations.Pages;

public partial class Configurations
{
    public List<ConfigurationVm> List { get; set; } = [];

    public Configurations()
    {
        View.Title = "Configurations";
    }

    protected override Task OnInitializedAsync()
    {
        var faker = new Faker<ConfigurationVm>()
            .RuleFor(x => x.Name, faker => faker.Commerce.ProductName())
            .RuleFor(x => x.Value, faker => faker.Commerce.Ean13())
            .RuleFor(x => x.Description, faker => faker.Lorem.Sentence())
            .RuleFor(x => x.LastModified, faker => faker.Date.Recent(30));

        List = faker.Generate(5);
        return base.OnInitializedAsync();
    }

    private void ButtonAction()
    {
    }
}