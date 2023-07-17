﻿using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;

namespace PaymentGateways.Api.HostedService;

public class SampleService : IHostedService
{

    public SampleService()
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