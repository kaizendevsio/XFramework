using FluentValidation;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using RBS.Core.DataAccess.Commands.Handlers;
using RBS.Core.PipelineBehaviors;

namespace RBS.Api.Installers
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