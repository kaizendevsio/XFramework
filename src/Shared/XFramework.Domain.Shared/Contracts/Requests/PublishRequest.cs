using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Base;

namespace XFramework.Domain.Shared.Contracts.Requests;

public record PublishRequest<T>(T? Data) : IHasRequestServer
{
    public RequestMetadata? Metadata { get; set; }
}