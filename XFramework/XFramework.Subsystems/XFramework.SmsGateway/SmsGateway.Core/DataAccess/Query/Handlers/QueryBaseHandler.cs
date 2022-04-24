using Microsoft.Extensions.Configuration;
using XFramework.Integration.Interfaces;
using XFramework.Integration.Interfaces.Wrappers;

namespace SmsGateway.Core.DataAccess.Query.Handlers;

public class QueryBaseHandler
{
    public IDataLayer _dataLayer;
    public ICachingService _cachingService;
    public IHelperService _helperService;
    public IConfiguration _configuration;
    public IJwtService _jwtService;
    public ILoggerWrapper _recordsService;
    public JwtOptionsBO _jwtOptions;

    public async Task<Application> GetApplication(Guid? guid)
    {
        if (guid is null) return null;
        var entity = await _dataLayer.Applications
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Guid == $"{guid}");
        if (entity is null)
        {
            throw new ArgumentException($"Application with Guid '{guid}' does not exist in any tenants");
        }
        return entity;
    } 

}