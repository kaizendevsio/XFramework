using System.Net;

namespace IdentityServer.Domain.BO
{
    public class RequestServerBO
    {
        public long ApplicationId { get; set; }
        public IPAddress IpAddress { get; set; }
    }
}