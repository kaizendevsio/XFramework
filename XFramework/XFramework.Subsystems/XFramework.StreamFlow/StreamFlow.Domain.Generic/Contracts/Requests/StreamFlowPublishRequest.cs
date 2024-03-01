using XFramework.Domain.Generic.Contracts.Requests;

namespace StreamFlow.Domain.Generic.Contracts.Requests;

public record StreamFlowPublishRequest<T>(T Model) : RequestBase;