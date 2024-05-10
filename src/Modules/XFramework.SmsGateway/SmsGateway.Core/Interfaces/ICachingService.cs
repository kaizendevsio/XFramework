using System.Collections.Concurrent;
using SmsGateway.Domain.Shared.Contracts.Responses.Sms;
using XFramework.Domain.Shared.Contracts;
using XFramework.Domain.Shared.Interfaces;

namespace SmsGateway.Core.Interfaces;

public interface ICachingService : IXFrameworkService
{
    public ConcurrentDictionary<Guid, SmsNodeJob> PendingMessageList { get; set; }
    public ConcurrentDictionary<Guid, SmsNodeJob> ScheduledMessageList { get; set; }
}