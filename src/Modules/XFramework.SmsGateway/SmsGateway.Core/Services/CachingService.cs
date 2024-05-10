using System.Collections.Concurrent;
using SmsGateway.Domain.Shared.Contracts.Responses.Sms;

namespace SmsGateway.Core.Services;

public class CachingService : ICachingService
{
    public ConcurrentDictionary<Guid, SmsNodeJob> PendingMessageList { get; set; } = [];
    public ConcurrentDictionary<Guid, SmsNodeJob> ScheduledMessageList { get; set; } = [];
}