using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using FluentValidation;
using Records.Core.DataAccess.Commands.Handlers;
using Records.Core.PipelineBehaviors;

namespace Records.Api.Installers
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