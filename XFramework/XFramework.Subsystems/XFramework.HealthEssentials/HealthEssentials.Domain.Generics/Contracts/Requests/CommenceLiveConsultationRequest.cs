namespace HealthEssentials.Domain.Generics.Contracts.Requests;

using TRequest = CommenceLiveConsultationRequest;
using TResponse = CmdResponse;

public record CommenceLiveConsultationRequest(Guid Id) : RequestBase,
    IRequest<TResponse>,
    IStreamflowRequest<TRequest, TResponse>;