using System.Net;
using StreamFlow.Domain.Generic.Enums;
using XFramework.Domain.Generic.Enums;
using MessagePack;
using XFramework.Domain.Generic.Contracts.Base;
using XFramework.Integration.Extensions;

namespace StreamFlow.Domain.Generic.Contracts.Requests;

public class StreamFlowMessage<T> : StreamFlowMessage
    where T : class, IHasRequestServer
{
    public StreamFlowMessage(T requestData)
    {
        RequestId = requestData.Metadata.RequestId ?? Guid.NewGuid();
        SetData(requestData);
    }

    public StreamFlowMessage()
    {
        RequestId = Guid.NewGuid();
    }
    
    private void SetData(T request)
    {
        CommandName ??= typeof(T).GetTypeFullName();
        Data = request;
    }
}

public class StreamFlowMessage
{
    public string Topic { get; set; }

    public string CommandName { get; set; }

    public object Data { get; set; }

    public string Message { get; set; }

    public Guid? RecipientId { get; set; }

    public Guid RequestId { get; set; }

    public Guid? ConsumerId { get; set; }
    
    public Guid ClientId { get; set; }

    public HttpStatusCode ResponseStatusCode { get; set; } = HttpStatusCode.Processing;

    public bool IsResponseSuccessful => (int)ResponseStatusCode < 300;

    public MessageExchangeType ExchangeType { get; set; } = MessageExchangeType.Direct;

    public GenericPriorityType PriorityType { get; set; } = GenericPriorityType.Information;
    
    public DateTime RequestDateTime { get; set; }
    
    public HttpStatusCode StreamFlowStatusCode { get; set; } = HttpStatusCode.OK;
}