using System.Net;

namespace XFramework.Domain.Generic.BusinessObjects
{
   public class HttpResponseBO
    {
        public CookieCollection ResponseCookies { get; set; }

        public string ResponseResult { get; set; }
    }
}
