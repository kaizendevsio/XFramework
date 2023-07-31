using System.Reflection;
using SmsGateway.Core.DataAccess.Commands.Handlers;
using SmsGateway.Core.PipelineBehaviors;
using FluentValidation;

namespace SmsGateway.Api.Installers;

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