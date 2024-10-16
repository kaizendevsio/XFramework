﻿using System.Net;

namespace XFramework.Domain.Shared.BusinessObjects;

public class HttpResponse<T>
{
    public CookieCollection Cookies { get; set; }

    public T Result { get; set; }
    public HttpStatusCode StatusCode { get; set; }
    public bool IsSuccess { get; set; }
    public string ReasonPhrase { get; set; }
}