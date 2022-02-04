using Microsoft.Extensions.Configuration;
using XFramework.Integration.Interfaces;
using XFramework.Integration.Interfaces.Wrappers;

namespace IdentityServer.Core.DataAccess.Query.Handlers;

public class QueryBaseHandler
{
    public IDataLayer _dataLayer;
    public ICachingService _cachingService;
    public IHelperService _helperService;
    public IConfiguration _configuration;
    public IJwtService _jwtService;
    public ILoggerWrapper _recordsService;
    public JwtOptionsBO _jwtOptions;


}