using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace XFramework.External.Models
{
   public class HttpResponseBO
    {
        public CookieCollection ResponseCookies { get; set; }

        public string ResponseResult { get; set; }
    }
}
