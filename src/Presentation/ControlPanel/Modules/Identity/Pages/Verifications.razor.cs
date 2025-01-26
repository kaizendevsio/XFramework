using Bogus;
using ControlPanel.Modules.Identity.ViewModels;

namespace ControlPanel.Modules.Identity.Pages;

public partial class Verifications
{
    public List<VerificationVm> List { get; set; } = [];
    
    public Verifications()
    {
        View.Title = "Verifications";
    }

    protected override Task OnInitializedAsync()
    {
        var faker = new Faker<VerificationVm>()
           
            .RuleFor(x => x.Type, faker => faker.PickRandom(new[] { "Email", "Phone" }))
            .RuleFor(x => x.Status, faker => faker.PickRandom(new[] { "Pending", "Verified", "Failed" }))
            .RuleFor(x => x.RequestedAt, faker => faker.Date.Past(1))
            .RuleFor(x => x.VerifiedAt, (faker, verification) => verification.Status == "Verified" ? faker.Date.Between(verification.RequestedAt, DateTime.Now) : DateTime.MinValue);
        List = faker.Generate(5);
        return base.OnInitializedAsync();
    }

    private void ButtonAction()
    {
        
    }

   

}
