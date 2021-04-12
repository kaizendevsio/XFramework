using Microsoft.Extensions.Configuration;
using Records.Core.Interfaces;
using Records.Domain.BusinessObjects;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Integration.Interfaces;

namespace Records.Core.DataAccess.Query.Handlers
{
    public class QueryBaseHandler
    {
        public IDataLayer _dataLayer;
        public ICachingService _cachingService;
        public IHelperService _helperService;
        public IConfiguration _configuration;
        public IJwtService _jwtService;
        public JwtOptionsBO _jwtOptions;


    }
}
