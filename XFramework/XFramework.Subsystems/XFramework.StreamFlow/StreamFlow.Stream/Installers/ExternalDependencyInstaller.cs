using FluentValidation;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using StreamFlow.Core.DataAccess.Commands.Handlers;
using StreamFlow.Core.PipelineBehaviors;
using CommandBaseHandler = StreamFlow.Stream.Services.Handlers.CommandBaseHandler;

namespace StreamFlow.Stream.Installers;

public class ExternalDependencyInstaller : IInstaller
{
    public virtual void InstallServices(IServiceCollection services, IConfiguration configuration)
    {
        // MediatR
        services.AddMediatR(o => o.RegisterServicesFromAssemblyContaining<CommandBaseHandler>());
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(BasePipelineBehavior<,>));

        // FluentValidation
        services.AddValidatorsFromAssembly(typeof(CommandBaseHandler).GetTypeInfo().Assembly);


    }
}