using System.Net;

namespace IdentityServer.Domain.BusinessObjects
{
    public class RequestServerBO
    {
        public long ApplicationId { get; set; }
        public string IpAddress { get; set; }
    }
}