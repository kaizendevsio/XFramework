using System.Net.Http.Json;
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
        public string ClientIpAddress { get; set; }
        public string ClientName { get; set; }
        public Guid? ApplicationId { get; set; }
        public Guid? TargetClient { get; set; }
        
        public async Task<CmdResponseBO> SendVoidAsync<TRequest, TResponse>(string commandName ,TRequest request)
        {
            var rs = await GetRequestServer();
            try
            {
                request.SetPropertyValue("RequestServer", rs);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            
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
            try
            {
                request.SetPropertyValue("RequestServer", rs);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            
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
            if (string.IsNullOrEmpty(ClientIpAddress))
            {
                try
                {
                    var jsonIpResponse = await new HttpClient().GetFromJsonAsync<JsonIpResponse>("https://jsonip.com/");
                    ClientIpAddress = jsonIpResponse?.IpAddress;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }

            ApplicationId ??= Guid.Parse(Configuration.GetValue<string>("Application:DefaultUID"));
            ClientName = !string.IsNullOrEmpty(ClientName)
                ? ClientName
                : Configuration.GetValue<string>("StreamFlowConfiguration:ClientName");
            
            return new()
            {
                ApplicationId = ApplicationId,
                Name = ClientName,
                IpAddress = ClientIpAddress,
                RequestId = Guid.NewGuid()
            };
        }
    }
}