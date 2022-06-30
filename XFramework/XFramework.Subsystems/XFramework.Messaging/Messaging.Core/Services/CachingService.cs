namespace Messaging.Core.Services;

public class CachingService : ICachingService
{
    public CachingService()
    {
        QueuedMessageList = new();
    }
    
    public List<MessageDirect> QueuedMessageList { get; set; }
}