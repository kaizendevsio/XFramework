using System.Diagnostics.CodeAnalysis;
using HealthEssentials.Domain.BusinessObjects;
using HealthEssentials.Domain.Contexts;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace HealthEssentials.Core.Interfaces;

public interface IDataLayer
{   
    public XnelSystemsContext XnelSystemsContext { get; set; }
    public XnelSystemsHealthEssentialsContext HealthEssentialsContext { get; set; }
        
}