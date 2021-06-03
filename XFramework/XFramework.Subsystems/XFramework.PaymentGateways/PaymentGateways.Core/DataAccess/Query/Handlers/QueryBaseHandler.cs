using Microsoft.Extensions.Configuration;
using PaymentGateways.Core.Interfaces;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Integration.Interfaces;
using XFramework.Integration.Interfaces.Wrappers;

namespace PaymentGateways.Core.DataAccess.Query.Handlers
{
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
}
