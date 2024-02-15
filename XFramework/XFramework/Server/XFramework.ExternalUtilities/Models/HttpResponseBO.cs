using System.Net;

namespace XFramework.External.Models;

public class HttpResponseBO
{
    public CookieCollection ResponseCookies { get; set; }

    public string ResponseResult { get; set; }
}