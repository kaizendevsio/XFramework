using XFramework.Domain.Shared.BusinessObjects;

namespace XFramework.Domain.Shared.Contracts.Requests;

public record PublishRequest<T>(T? Data) : IHasRequestServer
{
    public RequestMetadata? Metadata { get; set; }
}