using System.Diagnostics.CodeAnalysis;
using HealthEssentials.Domain.BusinessObjects;
using HealthEssentials.Domain.DataTransferObjects;
using HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace HealthEssentials.Core.Interfaces;

public interface IDataLayer
{   
    public XnelSystemsContext XnelSystemsContext { get; set; }
    public XnelSystemsHealthEssentialsContext HealthEssentialsContext { get; set; }
        
}