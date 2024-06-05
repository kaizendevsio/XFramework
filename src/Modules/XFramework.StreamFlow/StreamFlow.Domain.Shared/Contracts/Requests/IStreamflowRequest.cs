using MediatR;
using XFramework.Domain.Shared.BusinessObjects;

namespace StreamFlow.Domain.Shared.Contracts.Requests;

public interface IStreamflowRequest<TRequest, TResponse>;