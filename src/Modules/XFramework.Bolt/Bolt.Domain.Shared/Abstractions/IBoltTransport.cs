using System.Net;
using Bolt.Domain.Shared.BusinessObjects;
using Bolt.Domain.Shared.Contracts.Requests;

namespace Bolt.Domain.Shared.Abstractions;

public interface IBoltTransport
{
    Task<HttpStatusCode> Subscribe(BoltHubClient request);
    Task TelemetryCall();
    Task<HttpStatusCode> Register(BoltHubClient request);
    Task<HttpStatusCode> Push(BoltMessage request);
    Task<bool> Ping();
    Task<HttpStatusCode> InvokeResponse(BoltMessage request);
    Task InvokeResponseHandler(BoltMessage response);
}