using IdentityServer.Core.Interfaces;

namespace IdentityServer.Core.DataAccess.Query.Handlers
{
    public class QueryBaseHandler
    {
        public IDataLayer _dataLayer;
        public ICachingService _cachingService;
    }
}
