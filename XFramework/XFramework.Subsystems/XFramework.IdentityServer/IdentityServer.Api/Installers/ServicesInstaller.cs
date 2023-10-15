using IdentityServer.Core.DataAccess.Commands.Credentials;
using MediatR;
using XFramework.Core.DataAccess.Commands;
using XFramework.Domain.Generic.Contracts;

namespace IdentityServer.Api.Installers;

public class ServicesInstaller : IInstaller
{
    public virtual void InstallServices(IServiceCollection services, IConfiguration configuration)
    {
        /*services.AddSingleton<ICachingService, CachingService>();*/
        services.AddTransient<IRequestHandler<Create<IdentityCredential>, CmdResponse<IdentityCredential>>, Create>();
        services.AddTransient<IRequestHandler<Create<IdentityContact>, CmdResponse<IdentityContact>>, IdentityServer.Core.DataAccess.Commands.Contacts.Create>();
    }
}