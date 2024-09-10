using MediatR;
using Microsoft.AspNetCore.Components;
using XFramework.Blazor.Core.Features.Application;

namespace ControlPanel.Core.Services;

public class BootstrapService : IBootstrapService
{
    private IMediator Mediator { get; }
    private NavigationManager NavigationManager { get; }
    private DateTime _startTime;
    private DateTime _endTime;

    public BootstrapService(IMediator mediator, NavigationManager navigationManager)
    {
        Mediator = mediator;
        NavigationManager = navigationManager;

        Console.WriteLine("Bootstrap Service Initialized");
        Task.Run(StartAsync);
    }

    protected virtual async Task StartAsync()
    {
        try
        {
             _startTime = DateTime.Now;
            /*
            var tasks = new Task[2];

            Console.WriteLine("Restoring Application State");
            tasks[0] = Mediator.Send(new ApplicationState.RestoreStates());
            tasks[1] = Mediator.Send(new AppState.RestoreStates());

            await Task.WhenAll(tasks);

            await Mediator.Send(new ApplicationState.SetState(){StateRestored = true});*/

            Console.WriteLine("Restoring application state");

            //NavigationManager.NavigateTo("/");

            await Mediator.Send(new ApplicationState.SetState() { StateRestored = true });
            await Mediator.Publish(new ApplicationState.StateRestoredEvent());
    
            _endTime = DateTime.Now;
            Console.WriteLine($"Application state restored in {_endTime.Subtract(_startTime).TotalMilliseconds}ms");
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }
}