using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using XFramework.Domain.Generic.Interfaces;

namespace XFramework.Integration.Interfaces
{
    public interface ISignalRService : IXFrameworkService
    {
        public HubConnection Connection { get; set; }

        public Task<bool> EnsureConnection();

        public Task<HttpStatusCode> InvokeVoidAsync<T>(string methodName, T args1);
    }
    
    
}