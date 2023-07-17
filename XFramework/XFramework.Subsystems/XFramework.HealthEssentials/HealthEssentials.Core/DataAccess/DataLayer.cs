using HealthEssentials.Domain.BusinessObjects;
using HealthEssentials.Domain.DataTransferObjects;
using HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace HealthEssentials.Core.DataAccess;

public class DataLayer : IDataLayer
{
    public DataLayer(XnelSystemsContext xnelSystemsContext, XnelSystemsHealthEssentialsContext healthEssentialsContext)
    {
        XnelSystemsContext = xnelSystemsContext;
        HealthEssentialsContext = healthEssentialsContext;
    }

    public XnelSystemsContext XnelSystemsContext { get; set; }
    public XnelSystemsHealthEssentialsContext HealthEssentialsContext { get; set; }
}