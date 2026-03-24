using XFramework.Domain.Shared.Contracts.Requests;

namespace Bolt.Domain.Shared.Contracts.Requests;

public record BoltPublishRequest<T>(T Model) : RequestBase;