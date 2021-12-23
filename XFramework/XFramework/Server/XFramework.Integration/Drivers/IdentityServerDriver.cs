using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using IdentityServer.Domain.Generic.Contracts.Requests;
using IdentityServer.Domain.Generic.Contracts.Responses;
using Mapster;
using Microsoft.Extensions.Configuration;
using StreamFlow.Domain.Generic.Enums;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Domain.Generic.Contracts.Responses;
using XFramework.Integration.Interfaces.Wrappers;

namespace XFramework.Integration.Drivers
{
    public class IdentityServerDriver : DriverBase, IIdentityServiceWrapper
    {
        public IdentityServerDriver(IMessageBusWrapper messageBusDriver, IConfiguration configuration)
        {
            MessageBusDriver = messageBusDriver;
            Configuration = configuration;
            TargetClient = Guid.Parse(Configuration.GetValue<string>("StreamFlowConfiguration:Targets:IdentityServerService"));
        }

        public async Task<QueryResponseBO<AuthorizeIdentityContract>> AuthenticateCredential(AuthenticateCredentialRequest request)
        {
            return await SendAsync<AuthenticateCredentialRequest, AuthorizeIdentityContract>("Authenticate", request);
        }

        public async Task<CmdResponseBO> CreateCredential(CreateCredentialRequest request)
        {
            return await SendVoidAsync<CreateCredentialRequest, CmdResponseBO>("CreateCredential", request);
        }

        public async Task<CmdResponseBO> UpdateCredential(UpdateCredentialRequest request)
        {
            return await SendVoidAsync<UpdateCredentialRequest, CmdResponseBO>("UpdateCredential", request);
        }

        public async Task<CmdResponseBO> DeleteCredential(DeleteCredentialRequest request)
        {
            return await SendVoidAsync<DeleteCredentialRequest, CmdResponseBO>("DeleteCredential", request);
        }

        public async Task<CmdResponseBO> ChangePassword(ChangePasswordRequest request)
        {
            return await SendVoidAsync<ChangePasswordRequest, CmdResponseBO>("ChangePassword", request);
        }

        public async Task<QueryResponseBO<ExistenceContract>> CheckCredentialExistence(CheckCredentialExistenceRequest request)
        {
            return await SendAsync<CheckCredentialExistenceRequest, ExistenceContract>("CheckCredentialExistence", request);
        }

        public async Task<QueryResponseBO<ExistenceContract>> CheckIdentityExistence(CheckIdentityExistenceRequest request)
        {
            return await SendAsync<CheckIdentityExistenceRequest, ExistenceContract>("CheckIdentityExistence", request);
        }

        public async Task<QueryResponseBO<IdentityInfoContract>> GetIdentity(GetIdentityRequest request)
        {
            return await SendAsync<GetIdentityRequest, IdentityInfoContract>("GetIdentity", request);
        }

        public async Task<QueryResponseBO<List<IdentityCredentialContract>>> GetIdentityCredentialList(GetIdentityCredentialListRequest request)
        {
            return await SendAsync<GetIdentityCredentialListRequest, List<IdentityCredentialContract>>("GetIdentityCredentialList", request);
        }

        public async Task<QueryResponseBO<List<IdentityCredentialContract>>> GetIdentityRoleList(GetIdentityRoleListRequest request)
        {
            return await SendAsync<GetIdentityRoleListRequest, List<IdentityCredentialContract>>("GetIdentityRoleList", request);
        }

        public async Task<QueryResponseBO<List<IdentityRoleEntityContract>>> GetRoleEntityList(GetRoleEntityListRequest request)
        {
            return await SendAsync<GetRoleEntityListRequest, List<IdentityRoleEntityContract>>("GetRoleEntityList", request);
        }

        public async Task<CmdResponseBO> CreateIdentity(CreateIdentityRequest request)
        {
            return await SendVoidAsync<CreateIdentityRequest, CmdResponseBO>("CreateIdentity", request);
        }

        public async Task<CmdResponseBO> UpdateIdentity(UpdateIdentityRequest request)
        {
            return await SendVoidAsync<UpdateIdentityRequest, CmdResponseBO>("UpdateIdentity", request);
        }

        public async Task<CmdResponseBO> DeleteIdentity(DeleteIdentityRequest request)
        {
            return await SendVoidAsync<DeleteIdentityRequest, CmdResponseBO>("DeleteIdentity", request);
        }

        public async Task<CmdResponseBO> CreateContact(CreateContactRequest request)
        {
            return await SendVoidAsync<CreateContactRequest, CmdResponseBO>("CreateContact", request);
        }

        public async Task<CmdResponseBO> UpdateContact(UpdateContactRequest request)
        {
            return await SendVoidAsync<UpdateContactRequest, CmdResponseBO>("UpdateContact", request);
        }

        public async Task<CmdResponseBO> DeleteContact(DeleteContactRequest request)
        {
            return await SendVoidAsync<DeleteContactRequest, CmdResponseBO>("DeleteContact", request);
        }

        public async Task<QueryResponseBO<ExistenceContract>> CheckContactExistence(CheckContactExistenceRequest request)
        {
            return await SendAsync<CheckContactExistenceRequest, ExistenceContract>("CheckContactExistence", request);
        }
    }
}