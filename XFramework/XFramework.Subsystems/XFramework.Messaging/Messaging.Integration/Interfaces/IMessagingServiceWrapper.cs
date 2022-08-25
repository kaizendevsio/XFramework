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

    

    #endregion
    #region Message Delivery Entity

    

    #endregion
    #region Message Direct

    

    #endregion
    #region Message Files

    

    #endregion
    #region Message Reaction

    

    #endregion
    #region Message Reaction Entity



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

    

    #endregion
}