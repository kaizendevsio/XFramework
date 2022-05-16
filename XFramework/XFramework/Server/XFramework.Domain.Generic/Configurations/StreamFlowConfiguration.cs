using System;
using System.Collections.Generic;
using System.Text.Json;
using XFramework.Domain.Generic.Enums;

namespace XFramework.Domain.Generic.Configurations
{
    public class StreamFlowConfiguration
    {
        public List<Uri> ServerUrls { get; set; }
        public Guid ClientGuid { get; set; }
        public string ClientName { get; set; }
        public string ClientDescription { get; set; }
        public int ReconnectDelay { get; set; }
        public int MaxRetry { get; set; }
        public string Signature { get; set; }
        public int QueueDepth { get; set; }
        public bool QueueMessages { get; set; }
        public bool Anonymous { get; set; }
        
        
    }

}