using System.Net;

namespace XFramework.Domain.Generic.BusinessObjects;

public class CmdResponse<T> : IBaseResponse, IHasRequestServer
{
    public CmdResponse() { }
    
    public HttpStatusCode HttpStatusCode { get; set; }
    public string? Message { get; set; }
    public T? Response { get; set; }
    public bool IsSuccess => (int)HttpStatusCode >= 200 && (int)HttpStatusCode < 300;
    public RequestMetadata? Metadata { get; set; }
}
    
public class CmdResponse : IBaseResponse
{
    public CmdResponse() { }
    
    public HttpStatusCode HttpStatusCode { get; set; }
    public string? Message { get; set; }
    public bool IsSuccess => (int)HttpStatusCode >= 200 && (int)HttpStatusCode < 300;
}