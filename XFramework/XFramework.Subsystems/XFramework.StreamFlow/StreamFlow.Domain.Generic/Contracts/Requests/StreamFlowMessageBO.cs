﻿using System;
using System.Text.Json;
using BinaryPack;
using MessagePack;
using StreamFlow.Domain.Generic.Enums;
using XFramework.Domain.Generic.Enums;

namespace StreamFlow.Domain.Generic.Contracts.Requests
{
    public class StreamFlowMessageBO
    {
        public StreamFlowMessageBO()
        {
            
        }
        
        public void SetData<T>(T request) where T : new()
        {
            CommandName = request.GetType().Name.Replace("Request", string.Empty);
            Data = BinaryConverter.Serialize(request);
        }
        
        public string CommandName { get; set; }
        public byte[] Data { get; set; }
        public string Message { get; set; }
        public Guid? Recipient { get; set; }
        public Guid RequestGuid { get; set; } = Guid.NewGuid();
        public Guid? ConsumerGuid { get; set; }
        public MessageExchangeType ExchangeType { get; set; } = MessageExchangeType.FanOut;
        public GenericPriorityType PriorityType { get; set; } = GenericPriorityType.Information;
    }
}