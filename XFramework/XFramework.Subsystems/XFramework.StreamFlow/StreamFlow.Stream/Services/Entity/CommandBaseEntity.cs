using Microsoft.AspNetCore.SignalR;

namespace StreamFlow.Stream.Services.Entity;

public class CommandBaseEntity
{
    public RequestServer RequestServer { get; set; }
    public HubCallerContext Context { get; set; }
}