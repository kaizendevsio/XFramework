using Bogus;
using ControlPanel.Modules.Identity.ViewModels;

namespace ControlPanel.Modules.Finance.Pages;

public partial class Wallets
{
    public List<WalletVm> List { get; set; } = [];
    
    public Wallets()
    {
        View.Title = "Wallets";
    }

    protected override Task OnInitializedAsync()
    {
        var faker = new Faker<WalletVm>()
            .RuleFor(x => x.WalletType, faker => faker.PickRandom(new[] { "Savings", "Checking", "Investment", "Business" }))
            .RuleFor(x => x.Balance, faker => faker.Finance.Amount(0, 10000))
            .RuleFor(x => x.Currency, faker => faker.Finance.Currency().Code)
            .RuleFor(x => x.Status, faker => faker.PickRandom(new[] { "Active", "Inactive", "Frozen" }))
            .RuleFor(x => x.LastTransactionDate, faker => faker.Date.Recent(30));

        List = faker.Generate(5);
        return base.OnInitializedAsync();
    }

    private void ButtonAction()
    {
        
    }

   

}
