using XFramework.Domain.Generic.Contracts;

namespace HealthEssentials.Core.DataAccess.Query.Handlers;

public class QueryBaseHandler
{
    public IDataLayer _dataLayer;
    public IDataLayer _dataLayer2;
    public IDataLayer _dataLayer3;
    public IDataLayer _dataLayer4;
    public IDataLayer _dataLayer5;
        
    public async Task<Application> GetApplication(Guid? guid)
    {
        if (guid is null) return null;
        var entity = await _dataLayer.XnelSystemsContext.Applications
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Guid == $"{guid}");
        if (entity is null)
        {
            throw new ArgumentException($"Application with Guid '{guid}' does not exist in any tenants");
        }
        return entity;
    } 
}