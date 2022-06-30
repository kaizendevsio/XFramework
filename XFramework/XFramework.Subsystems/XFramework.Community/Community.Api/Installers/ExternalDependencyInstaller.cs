using System.Reflection;
using Community.Core.DataAccess.Commands.Handlers;
using Community.Core.PipelineBehaviors;
using FluentValidation;

namespace Community.Api.Installers;

public class ExternalDependencyInstaller : IInstaller
{
    public virtual void InstallServices(IServiceCollection services, IConfiguration configuration)
    {
        // MediatR
        services.AddMediatR(typeof(CommandBaseHandler).GetTypeInfo().Assembly);
            
        // FluentValidation
        services.AddValidatorsFromAssembly(typeof(CommandBaseHandler).GetTypeInfo().Assembly);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(BasePipelineBehavior<,>));
            
           
    }
}