using Audit.Api.Services;
using IdentityServer.Integration.Extensions;
using XFramework.Core.Extensions;
using XFramework.Domain.Shared.Interfaces;
using XFramework.Integration.Extensions;
namespace Audit.Api.Installers;
public sealed class ServicesInstaller : IInstaller
{
    public void InstallServices<TAssembly>(IServiceCollection services, IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        services.AddTenantResolver();
        services.AddTenantModuleFeatures();
        services.AddXFrameworkBoltClient(configuration, hostEnvironment: hostEnvironment);
        services.AddIdentityServerSessionValidation();
        services.AddScoped<AuditQueryService>();
    }
}
