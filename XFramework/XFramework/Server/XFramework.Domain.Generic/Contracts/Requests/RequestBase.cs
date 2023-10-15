using XFramework.Domain.Generic.BusinessObjects;

namespace XFramework.Domain.Generic.Contracts.Requests;

public record RequestBase : IHasRequestServer
{
    public RequestServer Metadata { get; set; } = new ();
    public Guid RequestId { get; set; }
}