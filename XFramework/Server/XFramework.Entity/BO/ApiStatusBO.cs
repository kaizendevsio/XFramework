using System;
using System.Collections.Generic;
using System.Text;

namespace XFramework.Data.BO
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
