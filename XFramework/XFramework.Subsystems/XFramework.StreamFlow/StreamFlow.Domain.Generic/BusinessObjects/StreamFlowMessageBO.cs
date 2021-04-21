using System;
using System.Text.Json;
using StreamFlow.Domain.Enums;
using XFramework.Domain.Generic.Enums;

namespace StreamFlow.Domain.Generic.BusinessObjects
{
    public class StreamFlowMessageBO
    {
        public StreamFlowMessageBO()
        {
            
        }
        public StreamFlowMessageBO(object request)
        {
            CommandName = request.GetType().Name.Replace("Request", string.Empty);
            Data = JsonSerializer.Serialize(request);
        }
        public string CommandName { get; set; }
        public string Data { get; set; }
        public string Message { get; set; }
        public Guid? Recipient { get; set; }
        public Guid? RequestGuid { get; set; } = Guid.NewGuid();
        public Guid? ConsumerGuid { get; set; }
        public MessageExchangeType ExchangeType { get; set; } = MessageExchangeType.FanOut;
        public GenericPriorityType PriorityType { get; set; } = GenericPriorityType.Information;
    }
}