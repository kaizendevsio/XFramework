namespace XFramework.Integration.Drivers
{
    public class DriverBase
    {
        protected IConfiguration Configuration { get; set; }
        public IMessageBusWrapper MessageBusDriver { get; set; }
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
            var result = await MessageBusDriver.InvokeAsync<QueryResponseBO<TResponse>>(new(request)
            {
                CommandName = commandName,
                ExchangeType = MessageExchangeType.Direct,
                Recipient = TargetClient
            });
            return result.Response.Adapt<QueryResponseBO<TResponse>>();
        }
    }
}