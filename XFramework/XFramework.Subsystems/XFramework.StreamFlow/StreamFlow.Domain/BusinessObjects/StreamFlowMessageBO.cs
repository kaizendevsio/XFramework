using System;
using StreamFlow.Domain.Enums;
using XFramework.Domain.Generic.Enums;

namespace StreamFlow.Domain.BusinessObjects
{
    public class StreamFlowMessageBO
    {
        public string Data { get; set; }
        public Guid Recipient { get; set; }
        public string Message { get; set; }
        public string MethodName { get; set; }
        public StreamFlowServiceBO StreamFlowService { get; set; } = new();
        public MessageExchangeType ExchangeType { get; set; } = MessageExchangeType.FanOut;
        public GenericPriorityType PriorityType { get; set; } = GenericPriorityType.Information;
    }
}