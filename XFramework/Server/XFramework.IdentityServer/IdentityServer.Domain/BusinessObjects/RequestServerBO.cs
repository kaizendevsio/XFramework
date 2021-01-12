using System.Net;

namespace IdentityServer.Domain.BusinessObjects
{
    public class RequestServerBO
    {
        public long ApplicationId { get; set; }
        public IPAddress IpAddress { get; set; }
    }
}