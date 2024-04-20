using System.Net;
using XFramework.Domain.Shared.Contracts.Base;

namespace XFramework.Domain.Shared.BusinessObjects;

public class CmdResponse<T> : CmdResponse
{
    public T? Response { get; set; }
}
    
public class CmdResponse : IBaseResponse, IHasRequestServer, ICmdResponse
{
    public CmdResponse() { }
    
    public HttpStatusCode HttpStatusCode { get; set; }
    public string? Message { get; set; }
    public bool IsSuccess => (int)HttpStatusCode >= 200 && (int)HttpStatusCode < 300;
    public RequestMetadata? Metadata { get; set; }
}

public interface ICmdResponse;