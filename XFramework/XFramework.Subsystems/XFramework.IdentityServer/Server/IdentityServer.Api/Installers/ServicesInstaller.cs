using System.Reflection;
using FluentValidation;
using IdentityServer.Core.DataAccess.Commands.Handlers;
using IdentityServer.Core.Interfaces;
using IdentityServer.Core.PipelineBehaviors;
using IdentityServer.Core.Services;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityServer.Api.Installers
{
    public class ServicesInstaller : IInstaller
    {
        public virtual void InstallServices(IServiceCollection services, IConfiguration configuration)
        {
            services.AddSingleton<ICachingService, CachingService>();
            services.AddSingleton<IHelperService, HelperService>();
            services.AddSingleton<IJwtService, JwtService>();
          
        }
    }
}