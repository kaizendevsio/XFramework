using XFramework.Domain.Generic.BusinessObjects;

namespace XFramework.Domain.Generic.Contracts.Base;

public interface IHasRequestServer
{
    public RequestMetadata? Metadata { get; set; }
}