using SmsGateway.Domain.Generic.Contracts.Responses.Sms;
using XFramework.Domain.Generic.Interfaces;

namespace SmsGateway.Core.Interfaces;

public interface ICachingService : IXFrameworkService
{
    public List<MessageDirectResponse> PendingMessageList { get; set; }
    public List<MessageDirectResponse> ScheduledMessageList { get; set; }
}