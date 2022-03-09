using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TypeSupport.Extensions;
using XFramework.Domain.Generic.Enums;

namespace XFramework.Integration.Drivers
{
    public class DriverBase
    {
        protected IConfiguration Configuration { get; set; }
        public IMessageBusWrapper MessageBusDriver { get; set; }
        public IXLogger Logger { get; set; }
        public HubConnectionState ConnectionState => MessageBusDriver.ConnectionState;
        public string ClientIpAddress { get; set; }
        public string ClientName { get; set; }
        public Guid? ApplicationId { get; set; }
        public Guid? TargetClient { get; set; }
        
        public async Task<CmdResponse> SendVoidAsync<TRequest>(string commandName ,TRequest request)
        {
            var rs = await GetRequestServer(request);
            try
            {
                request.SetPropertyValue("RequestServer", rs);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
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
            var rs = new RequestServerBO();
            try
            {
                if (request.ContainsProperty("RequestServer"))
                {
                    rs = await GetRequestServer(request);
                    request.SetPropertyValue("RequestServer", rs);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
            
            var result = await MessageBusDriver.InvokeAsync<QueryResponse<TResponse>>(new(request)
            {
                CommandName = commandName,
                ExchangeType = MessageExchangeType.Direct,
                Recipient = TargetClient
            });

            Task.Run(async () =>
            {
                if (Logger is not null)
                {
                    var serviceRequestLog = new ServiceRequestLog<TRequest, QueryResponse<TResponse>>(){Request = request, Response = result.Response};
                    await Logger.Log("Service Request", JsonSerializer.Serialize(serviceRequestLog), this, rs.RequestId , LogLevel.Trace, LogType.SystemMaintenance);
                }
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
                    Console.WriteLine(e.Message);
                    throw;
                }
            }

            ApplicationId ??= Guid.Parse(Configuration.GetValue<string>("Application:DefaultUID"));
            ClientName = !string.IsNullOrEmpty(ClientName)
                ? ClientName
                : Configuration.GetValue<string>("StreamFlowConfiguration:ClientName");
            
            var applicationId = ApplicationId;
            var requestId = Guid.NewGuid();
            var ipAddress = string.Empty;
            var deviceAgent = string.Empty;
            var clientName = string.Empty;
            
            if (request.ContainsProperty("RequestServer"))
            {
                var requestServer = request.GetPropertyValue("RequestServer").Adapt<RequestServerBO>();

                applicationId = requestServer?.ApplicationId ?? applicationId;
                requestId = requestServer?.RequestId ?? requestId;

                clientName = !string.IsNullOrEmpty(requestServer?.Name)
                    ? requestServer.Name
                    : ClientName;
                ipAddress = !string.IsNullOrEmpty(requestServer?.IpAddress)
                    ? requestServer.IpAddress
                    : ClientIpAddress;
            }

            return new()
            {
                DeviceAgent = deviceAgent,
                ApplicationId = applicationId,
                Name = clientName,
                IpAddress = ipAddress,
                RequestId = requestId
            };
        }
    }
}