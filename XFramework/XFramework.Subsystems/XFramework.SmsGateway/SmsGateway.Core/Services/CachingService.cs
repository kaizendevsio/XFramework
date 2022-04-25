using SmsGateway.Domain.Generic.Contracts.Responses.Sms;
using SmsGateway.Domain.Generic.Enums;

namespace SmsGateway.Core.Services;

public class CachingService : ICachingService
{
    public CachingService()
    {
        PendingMessageList = new()
        {
            new()
            {
                Recipient = "09171132288",
                Intent = "OTP",
                Message = "Test",
                Status = (int) SmsStatus.Queued,
                ParentMessage = null
            }
        };
        ScheduledMessageList = new();
    }

    public List<MessageDirectResponse> PendingMessageList { get; set; }
    public List<MessageDirectResponse> ScheduledMessageList { get; set; }
}