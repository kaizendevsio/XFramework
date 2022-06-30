using Community.Domain.Generic.Contracts.Requests.Create;
using Community.Domain.Generic.Contracts.Requests.Delete;
using Community.Domain.Generic.Contracts.Requests.Get;
using Community.Domain.Generic.Contracts.Requests.Update;
using Community.Domain.Generic.Contracts.Responses.Connection;
using Community.Domain.Generic.Contracts.Responses.Content;
using Community.Domain.Generic.Contracts.Responses.Identity;
using Community.Integration.Interfaces;
using Microsoft.Extensions.Configuration;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Integration.Drivers;
using XFramework.Integration.Interfaces.Wrappers;

namespace Community.Integration.Drivers;

public class CommunityServiceDriver : DriverBase, ICommunityServiceWrapper
{
    public CommunityServiceDriver(IMessageBusWrapper messageBusDriver, IConfiguration configuration)
    {
        MessageBusDriver = messageBusDriver;
        Configuration = configuration;
        TargetClient = Guid.Parse(Configuration.GetValue<string>("StreamFlowConfiguration:Targets:CommunityService"));
    }
    
    public async Task<QueryResponse<CommunityContentResponse>> GetContent(GetContentRequest request)
    {
        return await SendAsync<GetContentRequest, CommunityContentResponse>(request);
    }

    public async Task<QueryResponse<List<CommunityContentResponse>>> GetContentList(GetContentListRequest request)
    {
        return await SendAsync<GetContentListRequest, List<CommunityContentResponse>>(request);
    }

    public async Task<QueryResponse<List<CommunityIdentityResponse>>> GetIdentityList(GetIdentityListRequest request)
    {
        return await SendAsync<GetIdentityListRequest, List<CommunityIdentityResponse>>(request);
    }

    public async Task<QueryResponse<CommunityIdentityResponse>> GetIdentity(GetIdentityRequest request)
    {
        return await SendAsync<GetIdentityRequest, CommunityIdentityResponse>(request);
    }

    public async Task<CmdResponse> CreateIdentity(CreateIdentityRequest request)
    {
        return await SendVoidAsync(request);
    }

    public async Task<CmdResponse> UpdateIdentity(UpdateIdentityRequest request)
    {
        return await SendVoidAsync(request);
    }

    public async Task<CmdResponse> CreateContent(CreateContentRequest request)
    {
        return await SendVoidAsync(request);
    }

    public async Task<CmdResponse> UpdateContent(UpdateContentRequest request)
    {
        return await SendVoidAsync(request);
    }

    public async Task<CmdResponse> DeleteContent(DeleteContentRequest request)
    {
        return await SendVoidAsync(request);
    }

    public async Task<CmdResponse> CreateReaction(CreateReactionRequest request)
    {
        return await SendVoidAsync(request);
    }

    public async Task<CmdResponse> UpdateReaction(UpdateReactionRequest request)
    {
        return await SendVoidAsync(request);
    }

    public async Task<CmdResponse> DeleteReaction(DeleteReactionRequest request)
    {
        return await SendVoidAsync(request);
    }

    public async Task<QueryResponse<List<CommunityConnectionResponse>>> GetConnectionList(GetConnectionListRequest request)
    {
        return await SendAsync<GetConnectionListRequest, List<CommunityConnectionResponse>>(request);
    }

    public async Task<CmdResponse> CreateConnection(CreateConnectionRequest request)
    {
        return await SendVoidAsync(request);
    }

    public async Task<CmdResponse> DeleteConnection(DeleteConnectionRequest request)
    {
        return await SendVoidAsync(request);
    }
}   