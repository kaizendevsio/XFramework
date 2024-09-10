using FluentValidation;
using XFramework.Blazor.Entity.Validations;

namespace ControlPanel.Core.Installers;

public class ValidationServicesInstaller : IInstaller
{
    public virtual void InstallServices<TApp>(
        IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment hostEnvironment)
    {
        services.AddValidatorsFromAssemblyContaining<BaseValidator>();
    }
}