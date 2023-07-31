using Microsoft.AspNetCore.SignalR;
using XFramework.Domain.Generic.BusinessObjects;

namespace StreamFlow.Stream.Services.Entity;

public class CommandBaseEntity
{
    public RequestServerBO RequestServer { get; set; }
    public HubCallerContext Context { get; set; }
}