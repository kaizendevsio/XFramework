using StreamFlow.Stream.Hubs;

namespace StreamFlow.Stream.Extensions;

public static class ApplicationBuilderExtension
{
    public static WebApplication UseAppServices(this WebApplication app)
    {
        app.UseSignalRHubEndpoints();
        return app;
    }
    
    public static WebApplication UseSignalRHubEndpoints(this WebApplication app)
    {
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapHub<MessageQueueHub>("/stream-flow/queue");
        });
        return app;
    }
}