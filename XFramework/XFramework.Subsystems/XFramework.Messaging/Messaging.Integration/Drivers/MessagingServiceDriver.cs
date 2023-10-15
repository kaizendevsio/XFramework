using Messaging.Domain.Generic.Contracts.Requests.Create;
using Messaging.Domain.Generic.Contracts.Requests.Delete;
using Messaging.Domain.Generic.Contracts.Requests.Get;
using Messaging.Domain.Generic.Contracts.Requests.Update;
using Messaging.Domain.Generic.Contracts.Responses;
using Messaging.Integration.Interfaces;
using Microsoft.Extensions.Configuration;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Integration.Abstractions.Wrappers;
using XFramework.Integration.Drivers;

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

    public async Task<QueryResponse<MessageDeliveryResponse>> GetMessageDelivery(GetMessageDeliveryRequest request)
    {
        return await SendAsync<GetMessageDeliveryRequest, MessageDeliveryResponse>(request);
    }

    public async Task<QueryResponse<List<MessageDeliveryResponse>>> GetMessageDeliveryList(GetMessageDeliveryListRequest request)
    {
        return await SendAsync<GetMessageDeliveryListRequest, List<MessageDeliveryResponse>>(request);
    }

    public async Task<CmdResponse<CreateMessageDeliveryRequest>> CreateMessageDelivery(CreateMessageDeliveryRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<UpdateMessageDeliveryRequest>> UpdateMessageDelivery(UpdateMessageDeliveryRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<DeleteMessageDeliveryRequest>> DeleteMessageDelivery(DeleteMessageDeliveryRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<QueryResponse<MessageDeliveryEntityResponse>> GetMessageDeliveryEntity(GetMessageDeliveryEntityRequest request)
    {
        return await SendAsync<GetMessageDeliveryEntityRequest, MessageDeliveryEntityResponse>(request);
    }

    public async Task<QueryResponse<List<MessageDeliveryEntityResponse>>> GetMessageDeliveryEntityList(GetMessageDeliveryEntityListRequest request)
    {
        return await SendAsync<GetMessageDeliveryEntityListRequest, List<MessageDeliveryEntityResponse>>(request);
    }

    public async Task<CmdResponse<CreateMessageDeliveryEntityRequest>> CreateMessageDeliveryEntity(CreateMessageDeliveryEntityRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<UpdateMessageDeliveryEntityRequest>> UpdateMessageDeliveryEntity(UpdateMessageDeliveryEntityRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<DeleteMessageDeliveryEntityRequest>> DeleteMessageDeliveryEntity(DeleteMessageDeliveryEntityRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<QueryResponse<MessageDirectResponse>> GetMessageDirect(GetMessageDirectRequest request)
    {
        return await SendAsync<GetMessageDirectRequest, MessageDirectResponse>(request);
    }

    public async Task<QueryResponse<List<MessageDirectResponse>>> GetMessageDirectList(GetMessageDirectListRequest request)
    {
        return await SendAsync<GetMessageDirectListRequest, List<MessageDirectResponse>>(request);
    }

    public async Task<CmdResponse<CreateMessageDirectRequest>> CreateMessageDirect(CreateMessageDirectRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<UpdateMessageDirectRequest>> UpdateMessageDirect(UpdateMessageDirectRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<DeleteMessageDirectRequest>> DeleteMessageDirect(DeleteMessageDirectRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<QueryResponse<MessageFileResponse>> GetMessageFile(GetMessageFileRequest request)
    {
        return await SendAsync<GetMessageFileRequest, MessageFileResponse>(request);
    }

    public async Task<QueryResponse<List<MessageFileResponse>>> GetMessageFileList(GetMessageFileListRequest request)
    {
        return await SendAsync<GetMessageFileListRequest, List<MessageFileResponse>>(request);
    }

    public async Task<CmdResponse<CreateMessageFileRequest>> CreateMessageFile(CreateMessageFileRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<UpdateMessageFileRequest>> UpdateMessageFile(UpdateMessageFileRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<DeleteMessageFileRequest>> DeleteMessageFile(DeleteMessageFileRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<QueryResponse<MessageReactionResponse>> GetMessageReaction(GetMessageReactionRequest request)
    {
        return await SendAsync<GetMessageReactionRequest, MessageReactionResponse>(request);
    }

    public async Task<QueryResponse<List<MessageReactionResponse>>> GetMessageReactionList(GetMessageReactionListRequest request)
    {
        return await SendAsync<GetMessageReactionListRequest, List<MessageReactionResponse>>(request);
    }

    public async Task<CmdResponse<CreateMessageReactionRequest>> CreateMessageReaction(CreateMessageReactionRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<UpdateMessageReactionRequest>> UpdateMessageReaction(UpdateMessageReactionRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<DeleteMessageReactionRequest>> DeleteMessageReaction(DeleteMessageReactionRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<QueryResponse<MessageReactionEntityResponse>> GetMessageReactionEntity(GetMessageReactionEntityRequest request)
    {
        return await SendAsync<GetMessageReactionEntityRequest, MessageReactionEntityResponse>(request);
    }

    public async Task<QueryResponse<List<MessageReactionEntityResponse>>> GetMessageReactionEntityList(GetMessageReactionEntityListRequest request)
    {
        return await SendAsync<GetMessageReactionEntityListRequest, List<MessageReactionEntityResponse>>(request);
    }

    public async Task<CmdResponse<CreateMessageReactionEntityRequest>> CreateMessageReactionEntity(CreateMessageReactionEntityRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<UpdateMessageReactionEntityRequest>> UpdateMessageReactionEntity(UpdateMessageReactionEntityRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<DeleteMessageReactionEntityRequest>> DeleteMessageReactionEntity(DeleteMessageReactionEntityRequest request)
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

    public async Task<QueryResponse<MessageThreadEntityResponse>> GetMessageThreadEntity(GetMessageThreadEntityRequest request)
    {
        return await SendAsync<GetMessageThreadEntityRequest, MessageThreadEntityResponse>(request);
    }

    public async Task<QueryResponse<List<MessageThreadEntityResponse>>> GetMessageThreadEntityList(GetMessageThreadEntityListRequest request)
    {
        return await SendAsync<GetMessageThreadEntityListRequest, List<MessageThreadEntityResponse>>(request);
    }

    public async Task<CmdResponse<CreateMessageThreadEntityRequest>> CreateMessageThreadEntity(CreateMessageThreadEntityRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<UpdateMessageThreadEntityRequest>> UpdateMessageThreadEntity(UpdateMessageThreadEntityRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<DeleteMessageThreadEntityRequest>> DeleteMessageThreadEntity(DeleteMessageThreadEntityRequest request)
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

    public async Task<QueryResponse<MessageTypeResponse>> GetMessageType(GetMessageTypeRequest request)
    {
        return await SendAsync<GetMessageTypeRequest, MessageTypeResponse>(request);
    }

    public async Task<QueryResponse<List<MessageTypeResponse>>> GetMessageTypeList(GetMessageTypeListRequest request)
    {
        return await SendAsync<GetMessageTypeListRequest, List<MessageTypeResponse>>(request);
    }

    public async Task<CmdResponse<CreateMessageTypeRequest>> CreateMessageType(CreateMessageTypeRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<UpdateMessageTypeRequest>> UpdateMessageType(UpdateMessageTypeRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<DeleteMessageTypeRequest>> DeleteMessageType(DeleteMessageTypeRequest request)
    {
        return await SendAsync(request);
    }
}   