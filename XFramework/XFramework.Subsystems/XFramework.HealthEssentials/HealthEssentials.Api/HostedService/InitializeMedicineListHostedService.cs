using HealthEssentials.Core.DataAccess.Commands.Entity.Medicine;
using HealthEssentials.Integration.Interfaces;

namespace HealthEssentials.Api.HostedService;

public class InitializeMedicineListHostedService : IHostedService
{
    private IMediator Mediator { get; }

    public InitializeMedicineListHostedService(IMediator mediator)
    {
        Mediator = mediator;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await Mediator.Send(new InitializeMedicineListCmd(), CancellationToken.None);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        //throw new System.NotImplementedException();
        return Task.CompletedTask;
    }
}