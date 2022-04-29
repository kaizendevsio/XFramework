using XFramework.Domain.Generic.Interfaces;

namespace Messaging.Core.Interfaces;

public interface ICachingService : IXFrameworkService
{
    public List<MessageDirect> QueuedMessageList { get; set; }
}