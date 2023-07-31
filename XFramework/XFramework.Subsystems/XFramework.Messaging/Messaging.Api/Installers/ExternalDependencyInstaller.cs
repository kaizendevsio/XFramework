using System.Reflection;
using Messaging.Core.DataAccess.Commands.Handlers;
using Messaging.Core.PipelineBehaviors;
using FluentValidation;

namespace Messaging.Api.Installers;

public class ExternalDependencyInstaller : IInstaller
{
    public virtual void InstallServices(IServiceCollection services, IConfiguration configuration)
    {
        // MediatR
        services.AddMediatR(o => o.RegisterServicesFromAssemblyContaining<CommandBaseHandler>());
            
        // FluentValidation
        services.AddValidatorsFromAssembly(typeof(CommandBaseHandler).GetTypeInfo().Assembly);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(BasePipelineBehavior<,>));
            
           
    }
}