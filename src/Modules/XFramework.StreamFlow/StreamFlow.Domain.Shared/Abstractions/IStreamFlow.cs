using StreamFlow.Domain.Shared.BusinessObjects;
using StreamFlow.Domain.Shared.Contracts.Requests;

namespace StreamFlow.Domain.Shared.Abstractions;

public interface IStreamFlow
{
    Task Subscribe();
    Task TelemetryCall();
    Task Register(StreamFlowClient request);
    Task Push();
    Task<bool> Ping();
    Task<bool> InvokeResponse();
    Task InvokeResponseHandler(StreamFlowMessage response);
}