using XFramework.Client.Shared.Core.Features.Todo;

namespace XFramework.Administrator.Pages.Module;

public class DashboardBase : PageBase
{
    public DashboardBase()
    {
        View.Title = "Dashboard";
    }

    public TodoState TodoState => GetState<TodoState>();
    public async Task Display()
    {
        await Mediator.Send(new TodoState.CreateTodo());
    }

}