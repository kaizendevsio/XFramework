using System.Net;

namespace XFramework.Domain.Shared.BusinessObjects;

[MemoryPackable]
public partial class CmdResponse<T> : CmdResponse
{
    public T? Response { get; set; }
}

[MemoryPackable]
public partial class CmdResponse : IBaseResponse, IHasRequestServer, ICmdResponse
{
    [MemoryPackConstructor]
    public CmdResponse() { }
    
    public HttpStatusCode HttpStatusCode { get; set; }
    public string? Message { get; set; }
    public bool IsSuccess => (int)HttpStatusCode >= 200 && (int)HttpStatusCode < 300;
    public RequestMetadata? Metadata { get; set; }
}

public interface ICmdResponse;