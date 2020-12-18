using System;
using System.Collections.Generic;
using System.Text;

namespace XFramework.Domain.BO
{
   public class HostBO
    {
        public string Platform { get; set; }
        public string Version { get; set; }
        public bool Is64BitOperatingSystem { get; set; }
        public bool Is64BitProccess { get; set; }
        public string MachineName { get; set; }
        public int ProccessorCount { get; set; }
        public double SystemPageSize { get; set; }
        public double TickCount64 { get; set; }

    }
}
