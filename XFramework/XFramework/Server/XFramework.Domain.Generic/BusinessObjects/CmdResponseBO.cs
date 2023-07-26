using System.Net;

namespace XFramework.Domain.Generic.BusinessObjects;

public class CmdResponse<T>
{
    public CmdResponse() { }

    public CmdResponse(HttpStatusCode httpStatusCode, string? message = null)
    {
        HttpStatusCode = httpStatusCode;
        Message = message;
    }
    public HttpStatusCode HttpStatusCode { get; set; }
    public string? Message { get; set; }
    public T? Response { get; set; }
    public bool IsSuccess => (int)HttpStatusCode >= 200 && (int)HttpStatusCode < 300;
}
    
public class CmdResponse
{
    public CmdResponse() { }

    public CmdResponse(HttpStatusCode httpStatusCode, string? message = null)
    {
        HttpStatusCode = httpStatusCode;
        Message = message;
    }
    public HttpStatusCode HttpStatusCode { get; set; }
    public string? Message { get; set; }
    public bool IsSuccess => (int)HttpStatusCode >= 200 && (int)HttpStatusCode < 300;
}