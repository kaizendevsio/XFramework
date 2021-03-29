using Microsoft.Extensions.Configuration;
using StreamFlow.Core.Interfaces;
using StreamFlow.Domain.BusinessObjects;

namespace StreamFlow.Core.DataAccess.Query.Handlers
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
