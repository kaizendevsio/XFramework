using XFramework.Domain.Shared.BusinessObjects;

namespace XFramework.Domain.Shared.Contracts.Base;

public interface IHasRequestServer
{
    public RequestMetadata? Metadata { get; set; }
}