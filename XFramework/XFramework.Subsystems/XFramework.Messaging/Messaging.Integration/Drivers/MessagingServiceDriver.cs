using Messaging.Domain.Generic.Contracts.Requests.Create;
using Messaging.Domain.Generic.Contracts.Requests.Delete;
using Messaging.Domain.Generic.Contracts.Requests.Get;
using Messaging.Domain.Generic.Contracts.Requests.Update;
using Messaging.Domain.Generic.Contracts.Responses;
using Messaging.Integration.Interfaces;
using Microsoft.Extensions.Configuration;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Integration.Drivers;
using XFramework.Integration.Interfaces.Wrappers;

namespace Messaging.Integration.Drivers;

public class MessagingServiceDriver : DriverBase, IMessagingServiceWrapper
{
    public MessagingServiceDriver(IMessageBusWrapper messageBusDriver, IConfiguration configuration)
    {
        MessageBusDriver = messageBusDriver;
        Configuration = configuration;
        TargetClient = Guid.Parse(Configuration.GetValue<string>("StreamFlowConfiguration:Targets:MessagingService"));
    }

    public async Task<CmdResponse> CreateDirectMessage(CreateDirectMessageRequest request)
    {
        return await SendVoidAsync(request);
    }

    public async Task<CmdResponse> CreateVerificationMessage(CreateVerificationMessageRequest request)
    {
        return await SendVoidAsync(request);
    }

    public async Task<QueryResponse<MessageResponse>> GetMessage(GetMessageRequest request)
    {
        return await SendAsync<GetMessageRequest, MessageResponse>(request);
    }

    public async Task<QueryResponse<List<MessageResponse>>> GetMessageList(GetMessageListRequest request)
    {
        return await SendAsync<GetMessageListRequest, List<MessageResponse>>(request);
    }

    public async Task<CmdResponse<CreateMessageRequest>> CreateMessage(CreateMessageRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<UpdateMessageRequest>> UpdateMessage(UpdateMessageRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<DeleteMessageRequest>> DeleteMessage(DeleteMessageRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<QueryResponse<MessageThreadResponse>> GetMessageThread(GetMessageThreadRequest request)
    {
        return await SendAsync<GetMessageThreadRequest, MessageThreadResponse>(request);
    }

    public async Task<QueryResponse<List<MessageThreadResponse>>> GetMessageThreadList(GetMessageThreadListRequest request)
    {
        return await SendAsync<GetMessageThreadListRequest, List<MessageThreadResponse>>(request);
    }

    public async Task<CmdResponse<CreateMessageThreadRequest>> CreateMessageThread(CreateMessageThreadRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<UpdateMessageThreadRequest>> UpdateMessageThread(UpdateMessageThreadRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<DeleteMessageThreadRequest>> DeleteMessageThread(DeleteMessageThreadRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<QueryResponse<MessageThreadMemberResponse>> GetMessageThreadMember(GetMessageThreadMemberRequest request)
    {
        return await SendAsync<GetMessageThreadMemberRequest, MessageThreadMemberResponse>(request);
    }

    public async Task<QueryResponse<List<MessageThreadMemberResponse>>> GetMessageThreadMemberList(GetMessageThreadMemberListRequest request)
    {
        return await SendAsync<GetMessageThreadMemberListRequest, List<MessageThreadMemberResponse>>(request);
    }

    public async Task<CmdResponse<CreateMessageThreadMemberRequest>> CreateMessageThreadMember(CreateMessageThreadMemberRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<UpdateMessageThreadMemberRequest>> UpdateMessageThreadMember(UpdateMessageThreadMemberRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<DeleteMessageThreadMemberRequest>> DeleteMessageThreadMember(DeleteMessageThreadMemberRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<QueryResponse<MessageThreadMemberGroupResponse>> GetMessageThreadMemberGroup(GetMessageThreadMemberGroupRequest request)
    {
        return await SendAsync<GetMessageThreadMemberGroupRequest, MessageThreadMemberGroupResponse>(request);
    }

    public async Task<QueryResponse<List<MessageThreadMemberGroupResponse>>> GetMessageThreadMemberGroupList(GetMessageThreadMemberGroupListRequest request)
    {
        return await SendAsync<GetMessageThreadMemberGroupListRequest, List<MessageThreadMemberGroupResponse>>(request);
    }

    public async Task<CmdResponse<CreateMessageThreadMemberGroupRequest>> CreateMessageThreadMemberGroup(CreateMessageThreadMemberGroupRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<UpdateMessageThreadMemberGroupRequest>> UpdateMessageThreadMemberGroup(UpdateMessageThreadMemberGroupRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<DeleteMessageThreadMemberGroupRequest>> DeleteMessageThreadMemberGroup(DeleteMessageThreadMemberGroupRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<QueryResponse<MessageThreadMemberRoleResponse>> GetMessageThreadMemberRole(GetMessageThreadMemberRoleRequest request)
    {
        return await SendAsync<GetMessageThreadMemberRoleRequest, MessageThreadMemberRoleResponse>(request);
    }

    public async Task<QueryResponse<List<MessageThreadMemberRoleResponse>>> GetMessageThreadMemberRoleList(GetMessageThreadMemberRoleListRequest request)
    {
        return await SendAsync<GetMessageThreadMemberRoleListRequest, List<MessageThreadMemberRoleResponse>>(request);
    }

    public async Task<CmdResponse<CreateMessageThreadMemberRoleRequest>> CreateMessageThreadMemberRole(CreateMessageThreadMemberRoleRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<UpdateMessageThreadMemberRoleRequest>> UpdateMessageThreadMemberRole(UpdateMessageThreadMemberRoleRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<DeleteMessageThreadMemberRoleRequest>> DeleteMessageThreadMemberRole(DeleteMessageThreadMemberRoleRequest request)
    {
        return await SendAsync(request);
    }
}   