using System;

namespace XFramework.Domain.Generic.BusinessObjects
{
    public class RequestServerBO
    {
        public Guid? ApplicationId { get; set; }
        public string Name { get; set; }
        public string DeviceAgent { get; set; }
        public string IpAddress { get; set; }
        public Guid? RequestId { get; set; }
    }
}