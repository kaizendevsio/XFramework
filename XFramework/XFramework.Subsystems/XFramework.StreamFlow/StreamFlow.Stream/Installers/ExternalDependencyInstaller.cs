using FluentValidation;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using StreamFlow.Core.DataAccess.Commands.Handlers;
using StreamFlow.Core.PipelineBehaviors;

namespace StreamFlow.Stream.Installers
{
    public class ExternalDependencyInstaller : IInstaller
    {
        public virtual void InstallServices(IServiceCollection services, IConfiguration configuration)
        {
            // MediatR
            services.AddMediatR(typeof(CommandBaseHandler).GetTypeInfo().Assembly);
            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(BasePipelineBehavior<,>));

            // FluentValidation
            services.AddValidatorsFromAssembly(typeof(CommandBaseHandler).GetTypeInfo().Assembly);


        }
    }
}