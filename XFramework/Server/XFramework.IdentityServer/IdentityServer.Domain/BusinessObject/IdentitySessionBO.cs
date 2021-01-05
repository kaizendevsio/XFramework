using System;
using System.Net;
using IdentityServer.Domain.Enums;

namespace IdentityServer.Domain.BusinessObject
{
    public class IdentitySessionBO
    {
        public Guid Guid { get; set; }
        public DateTime LogonDateTime { get; set; }
        public TimeSpan MaxSessionTimeSpan { get; set; }
        public SessionState SessionState { get; set; }
        public IPAddress ClientIpAddress { get; set; }
        public RequestServerBO RequestServer { get; set; }
    }
}