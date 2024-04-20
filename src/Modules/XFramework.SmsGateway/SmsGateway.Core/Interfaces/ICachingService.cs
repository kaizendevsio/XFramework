using SmsGateway.Domain.Shared.Contracts.Responses.Sms;
using XFramework.Domain.Shared.Interfaces;

namespace SmsGateway.Core.Interfaces;

public interface ICachingService : IXFrameworkService
{
    public List<MessageDirectResponse> PendingMessageList { get; set; }
    public List<MessageDirectResponse> ScheduledMessageList { get; set; }
}