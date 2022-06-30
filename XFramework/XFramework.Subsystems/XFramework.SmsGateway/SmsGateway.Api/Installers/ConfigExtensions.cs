using XFramework.Domain.Generic.Interfaces;

namespace SmsGateway.Api.Installers;

public static class ConfigExtensions
{
    public static void InstallEndpointConfigInAssembly(this IApplicationBuilder app, IWebHostEnvironment env)
    {
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
        });
    }
    public static void WarmUpServices(this IApplicationBuilder app, IServiceCollection services, ServiceLifetime serviceLifetime)
    {
        foreach (var service in GetLifetime(services, serviceLifetime))
        {
            // may be registered more than once, so get all at once
            app.ApplicationServices.GetServices(service);
        }
    }
    static IEnumerable<Type> GetLifetime(IServiceCollection services, ServiceLifetime serviceLifetime)
    {
        var s = services
            .Where(descriptor => descriptor.Lifetime == serviceLifetime)
            .Where(descriptor => typeof(IXFrameworkService).IsAssignableFrom(descriptor.ServiceType) )//&& !descriptor.ServiceType.IsInterface && !descriptor.ServiceType.IsAbstract)
            .Where(descriptor => descriptor.ServiceType.ContainsGenericParameters == false)
            .Select(descriptor => descriptor.ServiceType)
            .Distinct();

        return s;
    }
}