﻿using System;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
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
            var options = new JsonSerializerOptions {ReferenceHandler = ReferenceHandler.IgnoreCycles};
            CommandName = request.GetType().Name.Replace("Request", string.Empty);
            Data = JsonSerializer.Serialize(request, options);
        }
        
        public string CommandName { get; set; }
        public string Data { get; set; }
        public string Message { get; set; }
        public Guid? Recipient { get; set; }
        public Guid RequestGuid { get; set; } = Guid.NewGuid();
        public Guid? ConsumerGuid { get; set; }
        public HttpStatusCode ResponseStatusCode { get; set; } = HttpStatusCode.OK;
        public bool IsResponseSuccessful => (int)ResponseStatusCode < 300;
        public MessageExchangeType ExchangeType { get; set; } = MessageExchangeType.FanOut;
        public GenericPriorityType PriorityType { get; set; } = GenericPriorityType.Information;
    }
}