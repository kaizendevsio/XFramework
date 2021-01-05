using System.Net;

namespace IdentityServer.Domain.BusinessObject
{
    public class RequestServerBO
    {
        public long ApplicationId { get; set; }
        public IPAddress IpAddress { get; set; }
    }
}