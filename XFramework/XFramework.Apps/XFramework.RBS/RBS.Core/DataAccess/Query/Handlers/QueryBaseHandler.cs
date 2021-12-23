using MediatR;
using Microsoft.Extensions.Configuration;
using RBS.Core.Interfaces;
using RBS.Domain.BusinessObjects;

namespace RBS.Core.DataAccess.Query.Handlers
{
    public class QueryBaseHandler
    {
        public IMediator _mediator;
        public IDataLayer _dataLayer;
        public ICachingService _cachingService;
        public IHelperService _helperService;
        public IConfiguration _configuration;
        public IJwtService _jwtService;
        public JwtOptionsBO _jwtOptions;


    }
}
