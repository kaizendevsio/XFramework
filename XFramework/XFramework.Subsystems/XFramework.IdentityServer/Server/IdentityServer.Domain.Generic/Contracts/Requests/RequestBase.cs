using XFramework.Domain.Generic.BusinessObjects;

namespace IdentityServer.Domain.Generic.Contracts.Requests
{
    public class RequestBase
    {
        public RequestServerBO RequestServer { get; set; } =  new ();
    }
}