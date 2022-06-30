namespace Community.Core.DataAccess.Commands.Handlers;

public class CommandBaseHandler
{
    public IDataLayer _dataLayer;

    public async Task<Application> GetApplication(Guid? guid)
    {
        if (guid is null) return null;
        var entity = await _dataLayer.Applications
            
            .AsSplitQuery()
            .FirstOrDefaultAsync(i => i.Guid == $"{guid}");
        if (entity is null)
        {
            throw new ArgumentException($"Application with Guid '{guid}' does not exist in any tenants");
        }
        return entity;
    } 
}