﻿using System;
using System.Net;

namespace StreamFlow.Domain.Generic.BusinessObjects
{
    public class StreamFlowTelemetryBO
    {
        public DateTime RequestDateTime { get; set; }
        public Guid RequestGuid { get; set; }
        public Guid? ClientGuid { get; set; }
        public Guid? ConsumerGuid { get; set; }
        public HttpStatusCode ResponseStatusCode { get; set; } = HttpStatusCode.OK;
    }
}