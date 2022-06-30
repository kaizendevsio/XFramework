using XFramework.Domain.Generic.Contracts.Requests;

namespace Community.Domain.Generic.Contracts.Requests.Get;

public class GetContentRequest : RequestBase
{
    public Guid? Guid { get; set; }
}