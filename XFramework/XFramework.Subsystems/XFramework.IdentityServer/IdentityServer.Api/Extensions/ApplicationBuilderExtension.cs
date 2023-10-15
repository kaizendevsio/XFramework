
namespace IdentityServer.Api.Extensions;

public static class ApplicationBuilderExtension
{
    public static WebApplication UseAppServices(this WebApplication app)
    {
        app.UseSignalRHubEndpoints();
        return app;
    }
    
    private static WebApplication UseSignalRHubEndpoints(this WebApplication app)
    {
      
        return app;
    }
}