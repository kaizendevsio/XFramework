using IdentityServer.Core.Interfaces;
using IdentityServer.Domain.BusinessObjects;
using IdentityServer.Integration.Interfaces;
using Microsoft.Extensions.Configuration;

namespace IdentityServer.Core.DataAccess.Query.Handlers
{
    public class QueryBaseHandler
    {
        public IDataLayer _dataLayer;
        public ICachingService _cachingService;
        public IHelperService _helperService;
        public IConfiguration _configuration;
        public IJwtService _jwtService;
        public IRecordsWrapper _recordsService;
        public JwtOptionsBO _jwtOptions;


    }
}
