using XFramework.Core.Interfaces;
using XFramework.Integration.Interfaces.Wrappers;

namespace XFramework.Core.DataAccess.Query.Handlers
{
    public class QueryBaseHandler
    {
        public IDataLayer _dataLayer;
        protected IIdentityServiceWrapper IdentityServiceWrapper;
    }
}
