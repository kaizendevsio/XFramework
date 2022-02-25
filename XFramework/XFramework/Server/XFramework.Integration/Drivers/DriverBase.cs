using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using TypeSupport.Extensions;

namespace XFramework.Integration.Drivers
{
    public class DriverBase
    {
        protected IConfiguration Configuration { get; set; }
        public IMessageBusWrapper MessageBusDriver { get; set; }
        public HubConnectionState ConnectionState => MessageBusDriver.ConnectionState;
        public Guid? TargetClient { get; set; }
        
        public async Task<CmdResponseBO> SendVoidAsync<TRequest, TResponse>(string commandName ,TRequest request)
        {
            var result = await MessageBusDriver.InvokeAsync<CmdResponseBO>(new(request)
            {
                CommandName = commandName,
                ExchangeType = MessageExchangeType.Direct,
                Recipient = TargetClient
            });
            return result.Response;
        }
        public async Task<QueryResponseBO<TResponse>> SendAsync<TRequest, TResponse>(string commandName ,TRequest request)
        {
            var rs = await GetRequestServer();
            request.SetPropertyValue();
            
            var result = await MessageBusDriver.InvokeAsync<QueryResponseBO<TResponse>>(new(request)
            {
                CommandName = commandName,
                ExchangeType = MessageExchangeType.Direct,
                Recipient = TargetClient
            });
            return result.Response.Adapt<QueryResponseBO<TResponse>>();
        }
        
        public async Task<RequestServerBO> GetRequestServer()
        {
            return new()
            {
                ApplicationId = Guid.Parse(Configuration.GetValue<string>("StreamFlowConfiguration:Targets:IdentityServerService")),
                Name = Configuration.GetValue<string>("StreamFlowConfiguration:ClientName"),
                IpAddress = HttpClient.ClientIdentity.IpAddress,
                RequestId = Guid.NewGuid()
            };
        }
    }
}