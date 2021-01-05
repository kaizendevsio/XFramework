using System;

namespace IdentityServer.Domain.BusinessObject
{
    public class ApiStatusBO
    {
        public string ApplicationName { get; set; }
        public string Status { get; set; }
        public DateTime StartupTime { get; set; }
        public string Environment { get; set; }
        public HostBO Host { get; set; }
    }
}