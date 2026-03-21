using System.Collections.Concurrent;
using SmsGateway.Domain.Shared.Contracts.Responses.Sms;

namespace SmsGateway.Api.Services;

public sealed class CachingService : ICachingService
{
    public ConcurrentDictionary<Guid, SmsNodeJob> PendingMessageList { get; set; } = [];
    public ConcurrentDictionary<Guid, SmsNodeJob> ScheduledMessageList { get; set; } = [];
}