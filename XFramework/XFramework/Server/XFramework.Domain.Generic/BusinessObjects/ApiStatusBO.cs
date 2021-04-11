using System;

namespace XFramework.Domain.Generic.BusinessObjects
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