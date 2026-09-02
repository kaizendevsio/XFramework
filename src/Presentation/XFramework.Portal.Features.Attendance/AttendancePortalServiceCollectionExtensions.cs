using Microsoft.Extensions.DependencyInjection;
using XFramework.Portal.Features.Attendance.Services;

namespace XFramework.Portal.Features.Attendance;

public static class AttendancePortalServiceCollectionExtensions
{
    public static IServiceCollection AddAttendancePortalFeature(this IServiceCollection services)
    {
        services.AddScoped<AttendancePortalReadService>();
        return services;
    }
}
