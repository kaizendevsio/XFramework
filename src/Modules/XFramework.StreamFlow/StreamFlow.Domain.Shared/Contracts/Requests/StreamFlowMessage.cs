using System.Net;
using MemoryPack;
using XFramework.Domain.Shared.Enums;
using MessagePack;
using StreamFlow.Domain.Shared.Enums;
using XFramework.Domain.Shared.Contracts.Base;
using XFramework.Domain.Shared.Extensions;

namespace StreamFlow.Domain.Shared.Contracts.Requests;

[MessagePackObject]
public class StreamFlowMessage<T> : StreamFlowMessage, IDisposable
    where T : class, IHasRequestServer
{
    public StreamFlowMessage(T requestData)
    {
        RequestId = requestData.Metadata?.RequestId ?? Guid.NewGuid();
        SetData(requestData);
    }

    public StreamFlowMessage()
    {
        RequestId = Guid.NewGuid();
    }
    
    private void SetData(T request)
    {
        CommandName ??= typeof(T).GetTypeFullName();
        Data = request is null ? [] : MemoryPackSerializer.Serialize(request);
    }

    public void Dispose()
    {
        Data = null;
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
    public string RecipientId { get; set; }

    [Key(5)]
    public Guid RequestId { get; set; }

    [Key(6)]
    public Guid? ConsumerId { get; set; }
    
    [Key(7)]
    public string ClientId { get; set; }

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
    
    [Key(14)] 
    public TimeSpan Duration { get; set; }
}