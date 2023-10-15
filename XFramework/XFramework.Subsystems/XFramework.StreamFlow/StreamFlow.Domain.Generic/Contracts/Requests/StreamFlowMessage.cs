using System.Net;
using StreamFlow.Domain.Generic.Enums;
using XFramework.Domain.Generic.Enums;
using MessagePack;

namespace StreamFlow.Domain.Generic.Contracts.Requests;

[MessagePackObject]
public class StreamFlowMessage<T> : StreamFlowMessage
    where T : new()
{
    public StreamFlowMessage(T requestData)
    {
        RequestId = Guid.NewGuid();
        SetData(requestData);
    }
        
    public StreamFlowMessage()
    {
        RequestId = Guid.NewGuid();
    }
    
    private void SetData(T request)
    {
        CommandName = typeof(T).Name;
        Data = MessagePackSerializer.Serialize(request);
    }
}

[MessagePackObject]
public class StreamFlowMessage
{
    [Key(0)]
    public string Topic { get; set; }

    [Key(1)]
    public string CommandName { get; set; }

    [Key(2)]
    public byte[] Data { get; set; }

    [Key(3)]
    public string Message { get; set; }

    [Key(4)]
    public Guid? RecipientId { get; set; }

    [Key(5)]
    public Guid RequestId { get; set; } = Guid.NewGuid();

    [Key(6)]
    public Guid? ConsumerId { get; set; }
    
    [Key(7)]
    public Guid ClientId { get; set; }

    [Key(8)]
    public HttpStatusCode ResponseStatusCode { get; set; } = HttpStatusCode.Processing;

    [Key(9)] public bool IsResponseSuccessful => (int)ResponseStatusCode < 300;

    [Key(10)]
    public MessageExchangeType ExchangeType { get; set; } = MessageExchangeType.Direct;

    [Key(11)]
    public GenericPriorityType PriorityType { get; set; } = GenericPriorityType.Information;
    
    [Key(12)]
    public DateTime RequestDateTime { get; set; }
    
    [Key(13)]
    public HttpStatusCode StreamFlowStatusCode { get; set; } = HttpStatusCode.OK;
}