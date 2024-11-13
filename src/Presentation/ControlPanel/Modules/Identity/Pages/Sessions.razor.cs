using Bogus;
using ControlPanel.Modules.Identity.ViewModels;

namespace ControlPanel.Modules.Identity.Pages;

public partial class Sessions
{
    public List<SessionVm> List { get; set; } = [];
    
    public Sessions()
    {
        View.Title = "Sessions";
    }

    protected override Task OnInitializedAsync()
    {
        var faker = new Faker<SessionVm>()
            .RuleFor(x => x.IpAddress, faker => faker.Internet.Ip())
            .RuleFor(x => x.Device, faker => faker.PickRandom(new[] { "Desktop", "Mobile", "Tablet" }))
            .RuleFor(x => x.Status, faker => faker.PickRandom(new[] { "Active", "Inactive", "Expired" }))
            .RuleFor(x => x.StartTime, faker => faker.Date.Recent(10))
            .RuleFor(x => x.EndTime, (faker, session) => session.Status == "Active" ? DateTime.MinValue : faker.Date.Between(session.StartTime, DateTime.Now));

        List = faker.Generate(5);
        return base.OnInitializedAsync();
    }

    private void ButtonAction()
    {
        
    }

   

}
