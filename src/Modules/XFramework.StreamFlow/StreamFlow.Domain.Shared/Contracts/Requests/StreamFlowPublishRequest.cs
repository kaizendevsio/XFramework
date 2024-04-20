using XFramework.Domain.Shared.Contracts.Requests;

namespace StreamFlow.Domain.Shared.Contracts.Requests;

public record StreamFlowPublishRequest<T>(T Model) : RequestBase;