using StreamFlow.Stream.Services;
using XFramework.Domain.Shared.Interfaces;

namespace StreamFlow.Stream.Installers;

public sealed class DependencyInstaller : IInstaller
{
    public void InstallServices<TApp>(IServiceCollection services, IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        services.AddScoped<IQueryExecutionService, QueryExecutionService>();
    }
}