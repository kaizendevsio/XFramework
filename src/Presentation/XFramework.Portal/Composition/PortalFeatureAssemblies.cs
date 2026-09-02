using System.Reflection;
using XFramework.Portal.Features.Attendance;
using XFramework.Portal.Features.Community;
using XFramework.Portal.Features.Storage;

namespace XFramework.Portal.Composition;

public static class PortalFeatureAssemblies
{
    public static Assembly[] All { get; } =
    [
        typeof(AttendancePortalFeature).Assembly,
        typeof(CommunityPortalFeature).Assembly,
        typeof(StoragePortalFeature).Assembly
    ];
}
