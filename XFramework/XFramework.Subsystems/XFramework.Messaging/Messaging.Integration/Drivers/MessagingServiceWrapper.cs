﻿using Messaging.Domain.Generic.Contracts.Requests.Create;
using XFramework.Domain.Generic.BusinessObjects;

namespace Messaging.Integration.Drivers;

public partial interface IMessagingServiceWrapper
{
    public Task<CmdResponse> CreateDirectMessage(CreateDirectMessageRequest request);
    public Task<CmdResponse> CreateVerificationMessage(CreateVerificationMessageRequest request);
}

public partial record MessagingServiceWrapper
{
    public Task<CmdResponse> CreateDirectMessage(CreateDirectMessageRequest request)
    {
        return SendVoidAsync(request);
    }

    public Task<CmdResponse> CreateVerificationMessage(CreateVerificationMessageRequest request)
    {
        return SendVoidAsync(request);
    }
}   