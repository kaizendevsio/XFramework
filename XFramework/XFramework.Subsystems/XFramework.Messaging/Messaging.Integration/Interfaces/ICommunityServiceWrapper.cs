using Messaging.Domain.Generic.Contracts.Requests.Create;
using Messaging.Domain.Generic.Contracts.Requests.Delete;
using Messaging.Domain.Generic.Contracts.Requests.Get;
using Messaging.Domain.Generic.Contracts.Requests.Update;
using Messaging.Domain.Generic.Contracts.Responses.Connection;
using Messaging.Domain.Generic.Contracts.Responses.Content;
using Messaging.Domain.Generic.Contracts.Responses.Identity;
using Microsoft.AspNetCore.SignalR.Client;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Domain.Generic.Interfaces;

namespace Messaging.Integration.Interfaces;

public interface ICommunityServiceWrapper : IXFrameworkService
{
    public HubConnectionState ConnectionState { get; }
    
    public Task<QueryResponse<CommunityContentResponse>> GetContent(GetContentRequest request);
    public Task<QueryResponse<List<CommunityContentResponse>>> GetContentList(GetContentListRequest request);
    
    public Task<QueryResponse<List<CommunityIdentityResponse>>> GetIdentityList(GetIdentityListRequest request);
    public Task<CmdResponse> CreateIdentity(CreateIdentityRequest request);
    public Task<CmdResponse> UpdateIdentity(UpdateIdentityRequest request);
    
    public Task<CmdResponse> CreateContent(CreateContentRequest request);
    public Task<CmdResponse> UpdateContent(UpdateContentRequest request);
    public Task<CmdResponse> DeleteContent(DeleteContentRequest request);
    
    public Task<CmdResponse> CreateReaction(CreateReactionRequest request);
    public Task<CmdResponse> UpdateReaction(UpdateReactionRequest request);
    public Task<CmdResponse> DeleteReaction(DeleteReactionRequest request);
    
    public Task<QueryResponse<List<CommunityConnectionResponse>>> GetConnectionList(GetConnectionListRequest request);
    public Task<CmdResponse> CreateConnection(CreateConnectionRequest request);
    public Task<CmdResponse> DeleteConnection(DeleteConnectionRequest request);
}