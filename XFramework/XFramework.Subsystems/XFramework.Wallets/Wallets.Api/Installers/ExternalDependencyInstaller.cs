using FluentValidation;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using Wallets.Core.DataAccess.Commands.Handlers;
using Wallets.Core.PipelineBehaviors;

namespace Wallets.Api.Installers
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