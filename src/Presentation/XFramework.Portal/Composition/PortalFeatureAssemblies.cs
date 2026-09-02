using System.Reflection;
using XFramework.Portal.Features.Attendance;
using XFramework.Portal.Features.Community;
using XFramework.Portal.Features.Communications;
using XFramework.Portal.Features.POS;
using XFramework.Portal.Features.Finance;
using XFramework.Portal.Features.Inventario;
using XFramework.Portal.Features.Storage;

namespace XFramework.Portal.Composition;

public static class PortalFeatureAssemblies
{
    public static Assembly[] All { get; } =
    [
        typeof(AttendancePortalFeature).Assembly,
        typeof(CommunityPortalFeature).Assembly,
        typeof(CommunicationsPortalFeature).Assembly,
        typeof(PosPortalFeature).Assembly,
        typeof(FinancePortalFeature).Assembly,
        typeof(InventarioPortalFeature).Assembly,
        typeof(StoragePortalFeature).Assembly
    ];
}
