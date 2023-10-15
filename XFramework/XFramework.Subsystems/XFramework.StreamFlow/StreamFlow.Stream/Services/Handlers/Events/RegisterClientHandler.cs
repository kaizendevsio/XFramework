﻿using MediatR;
using Microsoft.AspNetCore.Http.Connections.Features;
using Microsoft.AspNetCore.SignalR;
using StreamFlow.Core.Interfaces;
using StreamFlow.Stream.Hubs;
using StreamFlow.Stream.Services.Entity.Events;
using XFramework.Domain.Generic.Configurations;

namespace StreamFlow.Stream.Services.Handlers.Events;

public class RegisterClientHandler(
        ICachingService cachingService, 
        IHubContext<MessageQueueHub> hubContext,
        StreamFlowConfiguration streamFlowConfiguration)
    : IRequestHandler<RegisterClientCmd, CmdResponse<RegisterClientCmd>>
{
    private readonly Random _random = new();

    public async Task<CmdResponse<RegisterClientCmd>> Handle(RegisterClientCmd request, CancellationToken cancellationToken)
    {
        Again:
        var y = cachingService.Clients.TryAdd(_random.Next(100000000,999999999), new()
        {
            StreamId = request.Context.ConnectionId,
            Guid = request.Client.Guid,
            Name = request.Client.Name
        });
        if (!y)
        {
            goto Again;
        }
        RememberClient(request);

        Console.WriteLine($"Connection Registered with ID {request.Context.ConnectionId} : {request.Client.Guid} [{request.Context.Features.Get<IHttpTransportFeature>()?.TransportType.ToString()}] : {request.Client.Name}");
        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted
        };

    }
    private async Task RememberClient(RegisterClientCmd request)
    {
        if (cachingService.AbsoluteClients.All(i => i.Value.Guid != request.Client.Guid))
        {
            cachingService.AbsoluteClients.TryAdd(cachingService.AbsoluteClients.Count , new()
            {
                StreamId = request.Context.ConnectionId,
                Guid = request.Client.Guid,
                Name = request.Client.Name
            });
        }
        else
        {
            var client = cachingService.AbsoluteClients.FirstOrDefault(i => i.Value.Guid != request.Client.Guid);
            client.Value.StreamId = request.Context.ConnectionId;
        }
    }
}