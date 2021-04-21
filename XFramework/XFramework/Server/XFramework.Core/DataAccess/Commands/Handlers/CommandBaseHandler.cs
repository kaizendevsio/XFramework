using XFramework.Core.Interfaces;
using XFramework.Integration.Interfaces.Wrappers;

namespace XFramework.Core.DataAccess.Commands.Handlers
{
    public class CommandBaseHandler
    {
        protected IDataLayer DataLayer;
        protected IIdentityServiceWrapper IdentityServiceWrapper;
    }
}
