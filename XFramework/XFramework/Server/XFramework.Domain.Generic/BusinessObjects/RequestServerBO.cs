﻿using System;

namespace XFramework.Domain.Generic.BusinessObjects
{
    public class RequestServerBO
    {
        public long ApplicationId { get; set; }
        public string Name { get; set; }
        public string IpAddress { get; set; }
        public Guid Guid { get; set; }
    }
}