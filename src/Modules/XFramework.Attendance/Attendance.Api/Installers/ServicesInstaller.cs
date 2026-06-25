using Attendance.Api.Services;
using XFramework.Core.Extensions;
using XFramework.Domain.Shared.Interfaces;

namespace Attendance.Api.Installers;

public sealed class ServicesInstaller : IInstaller
{
    public void InstallServices<TAssembly>(
        IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment hostEnvironment)
    {
        services.AddTenantResolver();
        services.AddTenantModuleFeatures();
        services.AddScoped<AttendanceService>();
    }
}

