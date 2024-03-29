﻿using System.Reflection;
using SmsGateway.Domain.Generic.Contracts.Requests.Create;
using SmsGateway.Domain.Generic.Contracts.Requests.Get;
using SmsGateway.Integration.Interfaces;
using Microsoft.Extensions.Configuration;
using SmsGateway.Domain.Generic.Contracts.Responses.Sms;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Integration.Abstractions.Wrappers;
using XFramework.Integration.Drivers;
using XFramework.Integration.Security;

namespace SmsGateway.Integration.Drivers;

public record SmsGatewayServiceDriver : DriverBase, ISmsGatewayServiceWrapper
{
    public SmsGatewayServiceDriver(IMessageBusWrapper messageBusDriver, IConfiguration configuration)
    {
        MessageBusDriver = messageBusDriver;
        Configuration = configuration;
        
        var serviceName = Assembly.GetEntryAssembly()?.GetName().Name.Split(".").First() ?? throw new ArgumentException("Assembly name is not set");
        var serviceId = serviceName.ToSha256();
        TargetClient = serviceId;
    }


    public async Task<CmdResponse> CreateSmsMessage(CreateSmsMessageRequest request)
    {
        return await SendVoidAsync(request);
    }

    public async Task<QueryResponse<List<MessageDirectResponse>>> GetPendingSmsMessageList(GetPendingSmsMessageListRequest request)
    {
        return await SendAsync<GetPendingSmsMessageListRequest, List<MessageDirectResponse>>(request);
    }

    public async Task<QueryResponse<List<MessageDirectResponse>>> GetScheduledSmsMessageList(GetScheduledSmsMessageListRequest request)
    {
        return await SendAsync<GetScheduledSmsMessageListRequest, List<MessageDirectResponse>>(request);

    }
}   