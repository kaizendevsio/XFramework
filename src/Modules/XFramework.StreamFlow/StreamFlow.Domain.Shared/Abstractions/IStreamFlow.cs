using System.Net;
using StreamFlow.Domain.Shared.BusinessObjects;
using StreamFlow.Domain.Shared.Contracts.Requests;

namespace StreamFlow.Domain.Shared.Abstractions;

public interface IStreamFlow
{
    Task<HttpStatusCode> Subscribe(StreamFlowClient request);
    Task TelemetryCall();
    Task<HttpStatusCode> Register(StreamFlowClient request);
    Task<HttpStatusCode> Push(StreamFlowMessage request);
    Task<bool> Ping();
    Task<HttpStatusCode> InvokeResponse(StreamFlowMessage request);
    Task InvokeResponseHandler(StreamFlowMessage response);
}