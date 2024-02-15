using XFramework.Domain.Generic.BusinessObjects;

namespace XFramework.Domain.Generic.Contracts.Requests;

public record PublishRequest<T>(T? Data) : IHasRequestServer
{
    public RequestMetadata? Metadata { get; set; }
}