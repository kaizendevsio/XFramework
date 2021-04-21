using XFramework.Integration.Services.Helpers;

namespace IdentityServer.Api.SignalR.Handlers
{
    public class BaseSignalRHandler
    {
        public StopWatchHelper StopWatch { get; set; } = new();
    }
}