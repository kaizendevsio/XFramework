using Messaging.Domain.Generic.Contracts.Requests.Create;
using Messaging.Domain.Generic.Contracts.Requests.Delete;
using Messaging.Domain.Generic.Contracts.Requests.Get;
using Messaging.Domain.Generic.Contracts.Requests.Update;
using Messaging.Domain.Generic.Contracts.Responses;
using Microsoft.AspNetCore.SignalR.Client;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Domain.Generic.Interfaces;

namespace Messaging.Integration.Interfaces;

public interface IMessagingServiceWrapper : IXFrameworkService
{
    public HubConnectionState ConnectionState { get; }
    
    public Task<CmdResponse> CreateDirectMessage(CreateDirectMessageRequest request);
    public Task<CmdResponse> CreateVerificationMessage(CreateVerificationMessageRequest request);

    #region Message
    /// <summary>
    /// Gets the message profile.
    /// </summary>
    public Task<QueryResponse<MessageResponse>> GetMessage(GetMessageRequest request);
    /// <summary>
    ///  Get all messages in the system.
    /// </summary>
    public Task<QueryResponse<List<MessageResponse>>> GetMessageList(GetMessageListRequest request);
    /// <summary>
    ///  Creates a new message in the system.
    /// </summary>
    public Task<CmdResponse<CreateMessageRequest>> CreateMessage(CreateMessageRequest request);
    /// <summary>
    ///  Updates the message profile.
    /// </summary>
    public Task<CmdResponse<UpdateMessageRequest>> UpdateMessage(UpdateMessageRequest request);
    /// <summary>
    ///  Deletes the message from the system.
    /// </summary>
    public Task<CmdResponse<DeleteMessageRequest>> DeleteMessage(DeleteMessageRequest request);
    #endregion
    #region Message Delivery
    /// <summary>
    /// Gets the message delivery profile.
    /// </summary>
    public Task<QueryResponse<MessageDeliveryResponse>> GetMessageDelivery(GetMessageDeliveryRequest request);
    /// <summary>
    ///  Get all message deliveries in the system.
    /// </summary>
    public Task<QueryResponse<List<MessageDeliveryResponse>>> GetMessageDeliveryList(GetMessageDeliveryListRequest request);
    /// <summary>
    ///  Creates a new message delivery in the system.
    /// </summary>
    public Task<CmdResponse<CreateMessageDeliveryRequest>> CreateMessageDelivery(CreateMessageDeliveryRequest request);
    /// <summary>
    ///  Updates the message delivery profile.
    /// </summary>
    public Task<CmdResponse<UpdateMessageDeliveryRequest>> UpdateMessageDelivery(UpdateMessageDeliveryRequest request);
    /// <summary>
    ///  Deletes the message delivery from the system.
    /// </summary>
    public Task<CmdResponse<DeleteMessageDeliveryRequest>> DeleteMessageDelivery(DeleteMessageDeliveryRequest request);
    #endregion
    #region Message Delivery Entity
    /// <summary>
    /// Gets the message delivery entity profile.
    /// </summary>
    public Task<QueryResponse<MessageDeliveryEntityResponse>> GetMessageDeliveryEntity(GetMessageDeliveryEntityRequest request);
    /// <summary>
    ///  Get all message delivery entities in the system.
    /// </summary>
    public Task<QueryResponse<List<MessageDeliveryEntityResponse>>> GetMessageDeliveryEntityList(GetMessageDeliveryEntityListRequest request);
    /// <summary>
    ///  Creates a new message delivery entity in the system.
    /// </summary>
    public Task<CmdResponse<CreateMessageDeliveryEntityRequest>> CreateMessageDeliveryEntity(CreateMessageDeliveryEntityRequest request);
    /// <summary>
    ///  Updates the message delivery entity profile.
    /// </summary>
    public Task<CmdResponse<UpdateMessageDeliveryEntityRequest>> UpdateMessageDeliveryEntity(UpdateMessageDeliveryEntityRequest request);
    /// <summary>
    ///  Deletes the message delivery entity from the system.
    /// </summary>
    public Task<CmdResponse<DeleteMessageDeliveryEntityRequest>> DeleteMessageDeliveryEntity(DeleteMessageDeliveryEntityRequest request);
    #endregion
    #region Message Direct
    /// <summary>
    /// Gets the message direct profile.
    /// </summary>
    public Task<QueryResponse<MessageDirectResponse>> GetMessageDirect(GetMessageDirectRequest request);
    /// <summary>
    ///  Get all message directs in the system.
    /// </summary>
    public Task<QueryResponse<List<MessageDirectResponse>>> GetMessageDirectList(GetMessageDirectListRequest request);
    /// <summary>
    ///  Creates a new message direct in the system.
    /// </summary>
    public Task<CmdResponse<CreateMessageDirectRequest>> CreateMessageDirect(CreateMessageDirectRequest request);
    /// <summary>
    ///  Updates the message direct profile.
    /// </summary>
    public Task<CmdResponse<UpdateMessageDirectRequest>> UpdateMessageDirect(UpdateMessageDirectRequest request);
    /// <summary>
    ///  Deletes the message direct from the system.
    /// </summary>
    public Task<CmdResponse<DeleteMessageDirectRequest>> DeleteMessageDirect(DeleteMessageDirectRequest request);
    #endregion
    #region Message File
    /// <summary>
    /// Gets the message file profile.
    /// </summary>
    public Task<QueryResponse<MessageFileResponse>> GetMessageFile(GetMessageFileRequest request);
    /// <summary>
    ///  Get all message files in the system.
    /// </summary>
    public Task<QueryResponse<List<MessageFileResponse>>> GetMessageFileList(GetMessageFileListRequest request);
    /// <summary>
    ///  Creates a new message file in the system.
    /// </summary>
    public Task<CmdResponse<CreateMessageFileRequest>> CreateMessageFile(CreateMessageFileRequest request);
    /// <summary>
    ///  Updates the message file profile.
    /// </summary>
    public Task<CmdResponse<UpdateMessageFileRequest>> UpdateMessageFile(UpdateMessageFileRequest request);
    /// <summary>
    ///  Deletes the message file from the system.
    /// </summary>
    public Task<CmdResponse<DeleteMessageFileRequest>> DeleteMessageFile(DeleteMessageFileRequest request);
    #endregion
    #region Message Reaction
    /// <summary>
    /// Gets the message reaction profile.
    /// </summary>
    public Task<QueryResponse<MessageReactionResponse>> GetMessageReaction(GetMessageReactionRequest request);
    /// <summary>
    ///  Get all message reactions in the system.
    /// </summary>
    public Task<QueryResponse<List<MessageReactionResponse>>> GetMessageReactionList(GetMessageReactionListRequest request);
    /// <summary>
    ///  Creates a new message reaction in the system.
    /// </summary>
    public Task<CmdResponse<CreateMessageReactionRequest>> CreateMessageReaction(CreateMessageReactionRequest request);
    /// <summary>
    ///  Updates the message reaction profile.
    /// </summary>
    public Task<CmdResponse<UpdateMessageReactionRequest>> UpdateMessageReaction(UpdateMessageReactionRequest request);
    /// <summary>
    ///  Deletes the message reaction from the system.
    /// </summary>
    public Task<CmdResponse<DeleteMessageReactionRequest>> DeleteMessageReaction(DeleteMessageReactionRequest request);
    

    #endregion
    #region Message Reaction Entity
    /// <summary>
    /// Gets the message reaction entity profile.
    /// </summary>
    public Task<QueryResponse<MessageReactionEntityResponse>> GetMessageReactionEntity(GetMessageReactionEntityRequest request);
    /// <summary>
    ///  Get all message reaction entities in the system.
    /// </summary>
    public Task<QueryResponse<List<MessageReactionEntityResponse>>> GetMessageReactionEntityList(GetMessageReactionEntityListRequest request);
    /// <summary>
    ///  Creates a new message reaction entity in the system.
    /// </summary>
    public Task<CmdResponse<CreateMessageReactionEntityRequest>> CreateMessageReactionEntity(CreateMessageReactionEntityRequest request);
    /// <summary>
    ///  Updates the message reaction entity profile.
    /// </summary>
    public Task<CmdResponse<UpdateMessageReactionEntityRequest>> UpdateMessageReactionEntity(UpdateMessageReactionEntityRequest request);
    /// <summary>
    ///  Deletes the message reaction entity from the system.
    /// </summary>
    public Task<CmdResponse<DeleteMessageReactionEntityRequest>> DeleteMessageReactionEntity(DeleteMessageReactionEntityRequest request);
    #endregion
    #region Message Thread
    /// <summary>
    /// Gets the message thread profile.
    /// </summary>
    public Task<QueryResponse<MessageThreadResponse>> GetMessageThread(GetMessageThreadRequest request);
    /// <summary>
    ///  Get all message threads in the system.
    /// </summary>
    public Task<QueryResponse<List<MessageThreadResponse>>> GetMessageThreadList(GetMessageThreadListRequest request);
    /// <summary>
    ///  Creates a new message thread in the system.
    /// </summary>
    public Task<CmdResponse<CreateMessageThreadRequest>> CreateMessageThread(CreateMessageThreadRequest request);
    /// <summary>
    ///  Updates the message thread profile.
    /// </summary>
    public Task<CmdResponse<UpdateMessageThreadRequest>> UpdateMessageThread(UpdateMessageThreadRequest request);
    /// <summary>
    ///  Deletes the message thread from the system.
    /// </summary>
    public Task<CmdResponse<DeleteMessageThreadRequest>> DeleteMessageThread(DeleteMessageThreadRequest request);
    #endregion
    #region Message Thread Entity
    /// <summary>
    /// Gets the message thread entity profile.
    /// </summary>
    public Task<QueryResponse<MessageThreadEntityResponse>> GetMessageThreadEntity(GetMessageThreadEntityRequest request);
    /// <summary>
    ///  Get all message thread entities in the system.
    /// </summary>
    public Task<QueryResponse<List<MessageThreadEntityResponse>>> GetMessageThreadEntityList(GetMessageThreadEntityListRequest request);
    /// <summary>
    ///  Creates a new message thread entity in the system.
    /// </summary>
    public Task<CmdResponse<CreateMessageThreadEntityRequest>> CreateMessageThreadEntity(CreateMessageThreadEntityRequest request);
    /// <summary>
    ///  Updates the message thread entity profile.
    /// </summary>
    public Task<CmdResponse<UpdateMessageThreadEntityRequest>> UpdateMessageThreadEntity(UpdateMessageThreadEntityRequest request);
    /// <summary>
    ///  Deletes the message thread entity from the system.
    /// </summary>
    public Task<CmdResponse<DeleteMessageThreadEntityRequest>> DeleteMessageThreadEntity(DeleteMessageThreadEntityRequest request);
    #endregion
    #region Message Thread Member
    /// <summary>
    /// Gets the message thread member profile.
    /// </summary>
    public Task<QueryResponse<MessageThreadMemberResponse>> GetMessageThreadMember(GetMessageThreadMemberRequest request);
    /// <summary>
    ///  Get all message thread members in the system.
    /// </summary>
    public Task<QueryResponse<List<MessageThreadMemberResponse>>> GetMessageThreadMemberList(GetMessageThreadMemberListRequest request);
    /// <summary>
    ///  Creates a new message thread member in the system.
    /// </summary>
    public Task<CmdResponse<CreateMessageThreadMemberRequest>> CreateMessageThreadMember(CreateMessageThreadMemberRequest request);
    /// <summary>
    ///  Updates the message thread member profile.
    /// </summary>
    public Task<CmdResponse<UpdateMessageThreadMemberRequest>> UpdateMessageThreadMember(UpdateMessageThreadMemberRequest request);
    /// <summary>
    ///  Deletes the message thread member from the system.
    /// </summary>
    public Task<CmdResponse<DeleteMessageThreadMemberRequest>> DeleteMessageThreadMember(DeleteMessageThreadMemberRequest request);
    #endregion
    #region Message Thread Member Group
    /// <summary>
    /// Gets the message thread member group profile.
    /// </summary>
    public Task<QueryResponse<MessageThreadMemberGroupResponse>> GetMessageThreadMemberGroup(GetMessageThreadMemberGroupRequest request);
    /// <summary>
    ///  Get all message thread member groups in the system.
    /// </summary>
    public Task<QueryResponse<List<MessageThreadMemberGroupResponse>>> GetMessageThreadMemberGroupList(GetMessageThreadMemberGroupListRequest request);
    /// <summary>
    ///  Creates a new message thread member group in the system.
    /// </summary>
    public Task<CmdResponse<CreateMessageThreadMemberGroupRequest>> CreateMessageThreadMemberGroup(CreateMessageThreadMemberGroupRequest request);
    /// <summary>
    ///  Updates the message thread member group profile.
    /// </summary>
    public Task<CmdResponse<UpdateMessageThreadMemberGroupRequest>> UpdateMessageThreadMemberGroup(UpdateMessageThreadMemberGroupRequest request);
    /// <summary>
    ///  Deletes the message thread member group from the system.
    /// </summary>
    public Task<CmdResponse<DeleteMessageThreadMemberGroupRequest>> DeleteMessageThreadMemberGroup(DeleteMessageThreadMemberGroupRequest request);
    #endregion
    #region Message Thread Member Role
    /// <summary>
    /// Gets the message thread member role profile.
    /// </summary>
    public Task<QueryResponse<MessageThreadMemberRoleResponse>> GetMessageThreadMemberRole(GetMessageThreadMemberRoleRequest request);
    /// <summary>
    ///  Get all message thread member roles in the system.
    /// </summary>
    public Task<QueryResponse<List<MessageThreadMemberRoleResponse>>> GetMessageThreadMemberRoleList(GetMessageThreadMemberRoleListRequest request);
    /// <summary>
    ///  Creates a new message thread member role in the system.
    /// </summary>
    public Task<CmdResponse<CreateMessageThreadMemberRoleRequest>> CreateMessageThreadMemberRole(CreateMessageThreadMemberRoleRequest request);
    /// <summary>
    ///  Updates the message thread member role profile.
    /// </summary>
    public Task<CmdResponse<UpdateMessageThreadMemberRoleRequest>> UpdateMessageThreadMemberRole(UpdateMessageThreadMemberRoleRequest request);
    /// <summary>
    ///  Deletes the message thread member role from the system.
    /// </summary>
    public Task<CmdResponse<DeleteMessageThreadMemberRoleRequest>> DeleteMessageThreadMemberRole(DeleteMessageThreadMemberRoleRequest request);
    #endregion
    #region Message Type
    /// <summary>
    /// Gets the message type profile.
    /// </summary>
    public Task<QueryResponse<MessageTypeResponse>> GetMessageType(GetMessageTypeRequest request);
    /// <summary>
    ///  Get all message types in the system.
    /// </summary>
    public Task<QueryResponse<List<MessageTypeResponse>>> GetMessageTypeList(GetMessageTypeListRequest request);
    /// <summary>
    ///  Creates a new message type in the system.
    /// </summary>
    public Task<CmdResponse<CreateMessageTypeRequest>> CreateMessageType(CreateMessageTypeRequest request);
    /// <summary>
    ///  Updates the message type profile.
    /// </summary>
    public Task<CmdResponse<UpdateMessageTypeRequest>> UpdateMessageType(UpdateMessageTypeRequest request);
    /// <summary>
    ///  Deletes the message type from the system.
    /// </summary>
    public Task<CmdResponse<DeleteMessageTypeRequest>> DeleteMessageType(DeleteMessageTypeRequest request);
    #endregion
}