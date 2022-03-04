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
        
        public async Task<CmdResponse> SendVoidAsync<TRequest, TResponse>(string commandName ,TRequest request)
        {
            var rs = await GetRequestServer(request);
            try
            {
                request.SetPropertyValue("RequestServer", rs);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            
            var result = await MessageBusDriver.InvokeAsync<CmdResponse>(new(request)
            {
                CommandName = commandName,
                ExchangeType = MessageExchangeType.Direct,
                Recipient = TargetClient
            });
            return result.Response;
        }
        public async Task<QueryResponse<TResponse>> SendAsync<TRequest, TResponse>(string commandName ,TRequest request)
        {
            try
            {
                if (request.ContainsProperty("RequestServer"))
                {
                    var rs = await GetRequestServer(request);
                    request.SetPropertyValue("RequestServer", rs);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            
            var result = await MessageBusDriver.InvokeAsync<QueryResponse<TResponse>>(new(request)
            {
                CommandName = commandName,
                ExchangeType = MessageExchangeType.Direct,
                Recipient = TargetClient
            });
            return result.Response.Adapt<QueryResponse<TResponse>>();
        }
        
        public async Task<RequestServerBO> GetRequestServer<TRequest>(TRequest request)
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

            var applicationIdString = string.Empty;
            ApplicationId ??= Guid.Parse(Configuration.GetValue<string>("Application:DefaultUID"));
            var applicationId = ApplicationId;
            
            if (request.ContainsProperty("RequestServer"))
            {
                var rs = request.GetPropertyValue("RequestServer");
                applicationIdString = rs.GetPropertyValue("ApplicationId")?.ToString();
                if (!string.IsNullOrEmpty(applicationIdString))
                {
                    applicationId = Guid.Parse(applicationIdString);
                }
            }

            ClientName = !string.IsNullOrEmpty(ClientName)
                ? ClientName
                : Configuration.GetValue<string>("StreamFlowConfiguration:ClientName");
            
            return new()
            {
                ApplicationId = applicationId,
                Name = ClientName,
                IpAddress = ClientIpAddress,
                RequestId = Guid.NewGuid()
            };
        }
    }
}