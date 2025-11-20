using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Coins.Api.Installers
{
    public class ExternalDependencyInstaller : IInstaller
    {
        public virtual void InstallServices(IServiceCollection services, IConfiguration configuration)
        {
            // MediatR removed - migrated to VSA pattern
            // No external dependencies required for this module after VSA migration
        }
    }
}