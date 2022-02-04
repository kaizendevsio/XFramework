using System.Reflection;
using FluentValidation;
using MediatR;
using XFramework.Core.DataAccess.Commands.Handlers;
using XFramework.Core.PipelineBehaviors;

namespace XFramework.Api.Installers
{
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
}