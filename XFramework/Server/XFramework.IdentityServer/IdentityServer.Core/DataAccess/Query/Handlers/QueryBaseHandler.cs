using AutoMapper;
using IdentityServer.Core.Interfaces;

namespace IdentityServer.Core.DataAccess.Query.Handlers
{
    public class QueryBaseHandler
    {
        public IDataLayer _dataLayer;
        public IMapper _mapper;
    }
}
