using Microsoft.AspNetCore.SignalR.Client;
using XFramework.Domain.Generic.Interfaces;
using XFramework.Integration.Entity.Contracts.Responses;

namespace XFramework.Integration.Interfaces;

public interface ISignalRService : IXFrameworkService
{
    public HubConnection Connection { get; set; }

    public Task<bool> EnsureConnection();

    public Task<HttpStatusCode> InvokeVoidAsync<T>(string methodName, T args1);
    public Task<SignalRResponse> InvokeAsync<T>(T args1);
}