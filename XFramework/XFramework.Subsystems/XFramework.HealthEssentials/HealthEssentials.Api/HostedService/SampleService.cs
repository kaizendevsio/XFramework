using HealthEssentials.Integration.Interfaces;

namespace HealthEssentials.Api.HostedService;

public class SampleService : IHostedService
{

    public SampleService(IHealthEssentialsServiceWrapper wrapper)
    {
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        //throw new System.NotImplementedException();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        //throw new System.NotImplementedException();
        return Task.CompletedTask;
    }
}