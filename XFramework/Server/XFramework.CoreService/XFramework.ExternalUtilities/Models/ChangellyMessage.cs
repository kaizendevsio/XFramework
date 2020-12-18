using System;
using System.Collections.Generic;
using System.Text;

namespace XFramework.External.Models
{
   public class ChangellyMessage
    {
        public string jsonrpc { get; set; }
        public string id { get; set; }
        public string method { get; set; }
        //public ChangellyParams params { get; set; }
    }
}
