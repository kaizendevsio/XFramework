using Community.Domain.Generic.Contracts.Requests.Create;
using Community.Domain.Generic.Contracts.Requests.Delete;
using Community.Domain.Generic.Contracts.Requests.Get;
using Community.Domain.Generic.Contracts.Requests.Update;
using Community.Domain.Generic.Contracts.Responses.Content;
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
        return await SendAsync<GetContentRequest, CommunityContentResponse>("GetContent", request);
    }

    public async Task<QueryResponse<List<CommunityContentResponse>>> GetContentList(GetContentListRequest request)
    {
        return await SendAsync<GetContentListRequest, List<CommunityContentResponse>>("GetContentList", request);
    }

    public async Task<CmdResponse> CreateIdentity(CreateIdentityRequest request)
    {
        return await SendVoidAsync("CreateIdentity", request);
    }

    public async Task<CmdResponse> UpdateIdentity(UpdateIdentityRequest request)
    {
        return await SendVoidAsync("UpdateIdentity", request);
    }

    public async Task<CmdResponse> CreateContent(CreateContentRequest request)
    {
        return await SendVoidAsync("CreateContent", request);
    }

    public async Task<CmdResponse> UpdateContent(UpdateContentRequest request)
    {
        return await SendVoidAsync("UpdateContent", request);
    }

    public async Task<CmdResponse> DeleteContent(DeleteContentRequest request)
    {
        return await SendVoidAsync("DeleteContent", request);
    }
}