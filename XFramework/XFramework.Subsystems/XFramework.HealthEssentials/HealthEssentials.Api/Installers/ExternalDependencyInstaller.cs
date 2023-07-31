using System.Reflection;
using FluentValidation;
using HealthEssentials.Core.DataAccess.Commands.Handlers;
using HealthEssentials.Core.PipelineBehaviors;

namespace HealthEssentials.Api.Installers;

public class ExternalDependencyInstaller : IInstaller
{
    public virtual void InstallServices(IServiceCollection services, IConfiguration configuration)
    {
        // MediatR
        services.AddMediatR(o => o.RegisterServicesFromAssemblyContaining<CommandBaseHandler>());
            
        // FluentValidation
        services.AddValidatorsFromAssembly(typeof(CommandBaseHandler).GetTypeInfo().Assembly);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(BasePipelineBehavior<,>));
        
        // Mapster Settings
        TypeAdapterConfig.GlobalSettings.Default
            .PreserveReference(true)
            .IgnoreNullValues(true);
    }
}