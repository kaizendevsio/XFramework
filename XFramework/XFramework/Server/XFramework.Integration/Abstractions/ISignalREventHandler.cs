using MediatR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using XFramework.Integration.Drivers;
using XFramework.Integration.Services.Helpers;

namespace XFramework.Integration.Abstractions;

public interface ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator, ILogger<BaseSignalRHandler> logger, MetricsMonitor metricsMonitor);
}