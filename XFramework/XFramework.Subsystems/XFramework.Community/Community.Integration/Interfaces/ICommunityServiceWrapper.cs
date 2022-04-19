using Community.Domain.Generic.Contracts.Requests.Create;
using Community.Domain.Generic.Contracts.Requests.Delete;
using Community.Domain.Generic.Contracts.Requests.Get;
using Community.Domain.Generic.Contracts.Requests.Update;
using Community.Domain.Generic.Contracts.Responses.Content;
using Microsoft.AspNetCore.SignalR.Client;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Domain.Generic.Interfaces;

namespace Community.Integration.Interfaces;

public interface ICommunityServiceWrapper : IXFrameworkService
{
    public HubConnectionState ConnectionState { get; }
    
    public Task<QueryResponse<CommunityContentResponse>> GetContent(GetContentRequest request);
    public Task<QueryResponse<List<CommunityContentResponse>>> GetContentList(GetContentListRequest request);
    
    public Task<CmdResponse> CreateIdentity(CreateIdentityRequest request);
    public Task<CmdResponse> UpdateIdentity(UpdateIdentityRequest request);
    
    public Task<CmdResponse> CreateContent(CreateContentRequest request);
    public Task<CmdResponse> UpdateContent(UpdateContentRequest request);
    public Task<CmdResponse> DeleteContent(DeleteContentRequest request);
}