﻿using Messaging.Domain.Shared.Enums;
using SmsGateway.Domain.Shared.Contracts.Responses.Sms;

namespace SmsGateway.Core.Services;

public class CachingService : ICachingService
{
    public CachingService()
    {
        PendingMessageList = new();
        ScheduledMessageList = new();
    }

    public List<MessageDirectResponse> PendingMessageList { get; set; }
    public List<MessageDirectResponse> ScheduledMessageList { get; set; }
}