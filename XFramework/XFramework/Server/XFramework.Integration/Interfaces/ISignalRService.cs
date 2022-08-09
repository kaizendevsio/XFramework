using Microsoft.AspNetCore.SignalR.Client;
using StreamFlow.Domain.Generic.Contracts.Requests;
using XFramework.Domain.Generic.Configurations;
using XFramework.Domain.Generic.Interfaces;
using XFramework.Integration.Entity.Contracts.Responses;

namespace XFramework.Integration.Interfaces;

public interface ISignalRService : IXFrameworkService
{
    public HubConnection Connection { get; set; }
    public StreamFlowConfiguration StreamFlowConfiguration { get; set; }

    public Task<bool> EnsureConnection();

    public Task HandleSubscriptionsEvent(CredentialResponse credentialResponse);
    public Task<HttpStatusCode> InvokeVoidAsync(string methodName, StreamFlowMessageBO args1);
    public Task<SignalRResponse> InvokeAsync(StreamFlowMessageBO args1);
}