﻿using System.Reflection;
using FluentValidation;
using IdentityServer.Core.DataAccess.Commands.Handlers;
using IdentityServer.Core.PipelineBehaviors;

namespace IdentityServer.Api.Installers;

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