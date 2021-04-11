using System;
using System.Net;

namespace StreamFlow.Domain.BusinessObjects
{
    public class RequestServerBO
    {
        public long ApplicationId { get; set; }
        public string IpAddress { get; set; }
        public Guid ClientGuid { get; set; }
    }
}