using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XFramework.Integration.Drivers;
using XFramework.Integration.Services;

namespace XFramework.Integration.Abstractions;

public interface ISignalREventHandler
{
    public void Handle(HubConnection connection, ICommandQueryDispatcher dispatcher, ILogger<BaseSignalRHandler> logger, IServiceScopeFactory scopeFactory);
}