using Microsoft.AspNetCore.SignalR;

namespace StreamFlow.Stream.Services.Entity;

public class CommandBaseEntity
{
    public RequestMetadata RequestMetadata { get; set; }
    public HubCallerContext Context { get; set; }
}