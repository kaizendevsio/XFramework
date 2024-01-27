using StreamFlow.Stream.Hubs;

namespace StreamFlow.Stream.Extensions;

public static class ApplicationBuilderExtension
{
    public static IApplicationBuilder UseAppServices(this IApplicationBuilder appBuilder)
    {
        appBuilder.UseSignalRHubEndpoints();
        return appBuilder;
    }
    
    public static IApplicationBuilder UseSignalRHubEndpoints(this IApplicationBuilder appBuilder)
    {
        var app = appBuilder as WebApplication;
        app.MapHub<MessageQueueHub>("/stream-flow/queue");
        
        return app as IApplicationBuilder;
    }
}