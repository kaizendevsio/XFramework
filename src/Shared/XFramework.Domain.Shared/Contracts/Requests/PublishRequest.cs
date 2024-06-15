using XFramework.Domain.Shared.BusinessObjects;

namespace XFramework.Domain.Shared.Contracts.Requests;

[MemoryPackable]
public partial record PublishRequest<T>(T? Data) : IHasRequestServer
{
    public RequestMetadata? Metadata { get; set; }
}