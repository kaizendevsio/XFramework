using System;
using System.Reflection;
using System.Runtime.Versioning;
using IdentityServer.Core.DataAccess.Commands.Entity.Identity.Credential;
using IdentityServer.Domain.Generic.Contracts.Requests;
using MediatR;
using Microsoft.AspNetCore.SignalR.Client;
using StreamFlow.Domain.Generic.BusinessObjects;
using StreamFlow.Domain.Generic.Contracts.Requests;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Integration.Services.Helpers;

namespace IdentityServer.Api.SignalR.Handlers
{
    public class TestSignalRHandler : BaseSignalRHandler, ISignalREventHandler
    {
        public void Handle(HubConnection connection, IMediator mediator)
        {
            connection.On<string, string, StreamFlowTelemetryBO>(GetType().Name.Replace("Handler", string.Empty),
                async (data, message, telemetry) =>
                {
                    StopWatch.Start("Invoked");
                    try
                    {
                        Console.WriteLine("Sample Invocation Received");
                        var apiStatus = new ApiStatusBO
                        {
                            ApplicationName = Assembly.GetEntryAssembly()?.GetName().Name?.Split(".")[0],
                            StartupTime = DateTime.Now,
                            Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
                            Host = new HostBO
                            {
                                Platform = Environment.OSVersion.Platform.ToString(),
                                MachineName = Environment.MachineName,
                                ProccessorCount = Environment.ProcessorCount,
                                Is64BitOperatingSystem = Environment.Is64BitOperatingSystem,
                                Is64BitProccess = Environment.Is64BitProcess,
                                SystemPageSize = Environment.SystemPageSize,
                                TickCount64 = Environment.TickCount64,
                                Version = Environment.OSVersion.ToString(),
                                RuntimeVersion = Assembly.GetEntryAssembly()?.GetCustomAttribute<TargetFrameworkAttribute>()
                                    ?.FrameworkName
                            },
                            Status = "Running"
                        };
                        await RespondToInvoke(connection, telemetry, apiStatus);
                    }
                    catch (Exception e)
                    {
                        StopWatch.Stop($"[{DateTime.Now}] Invoked '{GetType().Name}' resulted in exception {e.Message}");
                    }
                });
        }
    }
}