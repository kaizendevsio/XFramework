using Bogus;
using ControlPanel.Modules.Identity.ViewModels;

namespace ControlPanel.Modules.Finance.Pages;

public partial class Transactions
{
    public List<TransactionVm> List { get; set; } = [];
    
    public Transactions()
    {
        View.Title = "Transactions";
    }

    protected override Task OnInitializedAsync()
    {
        var faker = new Faker<TransactionVm>()
            .RuleFor(x => x.Type, faker => faker.PickRandom(new[] { "Cash-in", "Cash-out", "Transfer" }))
            .RuleFor(x => x.Amount, faker => faker.Finance.Amount(1, 5000))
            .RuleFor(x => x.Date, faker => faker.Date.Recent(30))
            .RuleFor(x => x.Status, faker => faker.PickRandom(new[] { "Completed", "Pending", "Failed" }));

        List = faker.Generate(5);
        return base.OnInitializedAsync();
    }

    private void ButtonAction()
    {
        
    }

   

}
