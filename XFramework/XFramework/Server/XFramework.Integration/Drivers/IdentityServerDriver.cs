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

        public async Task<QueryResponseBO<AuthorizeIdentityResponse>> AuthenticateCredential(AuthenticateCredentialRequest request)
        {
            return await SendAsync<AuthenticateCredentialRequest, AuthorizeIdentityResponse>("Authenticate", request);
        }

        public async Task<QueryResponseBO<CredentialResponse>> GetCredential(GetCredentialRequest request)
        {
            return await SendAsync<GetCredentialRequest, CredentialResponse>("GetCredential", request);
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

        public async Task<CmdResponseBO> ChangePassword(UpdatePasswordRequest request)
        {
            return await SendVoidAsync<UpdatePasswordRequest, CmdResponseBO>("ChangePassword", request);
        }
        
        public async Task<CmdResponseBO> SendOneTimePassword(CheckOneTimePasswordRequest request)
        {
            return await SendVoidAsync<CheckOneTimePasswordRequest, CmdResponseBO>("SendOneTimePassword", request);
        }

        public async Task<QueryResponseBO<RoleResponse>> GetRole(GetRoleRequest request)
        {
            return await SendAsync<GetRoleRequest, RoleResponse>("GetRole", request);
        }

        public async Task<QueryResponseBO<ExistenceResponse>> CheckCredentialExistence(CheckCredentialExistenceRequest request)
        {
            return await SendAsync<CheckCredentialExistenceRequest, ExistenceResponse>("CheckCredentialExistence", request);
        }

        public async Task<QueryResponseBO<ExistenceResponse>> CheckIdentityExistence(CheckIdentityExistenceRequest request)
        {
            return await SendAsync<CheckIdentityExistenceRequest, ExistenceResponse>("CheckIdentityExistence", request);
        }

        public async Task<QueryResponseBO<IdentityResponse>> GetIdentity(GetIdentityRequest request)
        {
            return await SendAsync<GetIdentityRequest, IdentityResponse>("GetIdentity", request);
        }

        public async Task<QueryResponseBO<List<CredentialResponse>>> LegacyGetCredentialList(GetCredentialListRequest request)
        {
            return await SendAsync<GetCredentialListRequest, List<CredentialResponse>>("LegacyGetCredentialList", request);
        }

        public async Task<QueryResponseBO<List<CredentialResponse>>> GetCredentialList(GetCredentialListRequest request)
        {
            return await SendAsync<GetCredentialListRequest, List<CredentialResponse>>("GetCredentialList", request);
        }

        public async Task<QueryResponseBO<List<RoleResponse>>> GetRoleList(GetRoleListRequest request)
        {
            return await SendAsync<GetRoleListRequest, List<RoleResponse>>("GetRoleList", request);
        }

        public async Task<QueryResponseBO<RoleEntityResponse>> GetRoleEntity(GetRoleEntityRequest request)
        {
            return await SendAsync<GetRoleEntityRequest, RoleEntityResponse>("GetRoleEntity", request);
        }

        public async Task<QueryResponseBO<List<RoleEntityResponse>>> GetRoleEntityList(GetRoleEntityListRequest request)
        {
            return await SendAsync<GetRoleEntityListRequest, List<RoleEntityResponse>>("GetRoleEntityList", request);
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

        public async Task<QueryResponseBO<ContactResponse>> GetContact(GetContactRequest request)
        {
            return await SendAsync<GetContactRequest, ContactResponse>("GetContact", request);
        }

        public async Task<QueryResponseBO<List<ContactResponse>>> GetContactList(GetContactListRequest request)
        {
            return await SendAsync<GetContactListRequest, List<ContactResponse>>("GetContactList", request);
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

        public async Task<QueryResponseBO<ExistenceResponse>> CheckContactExistence(CheckContactExistenceRequest request)
        {
            return await SendAsync<CheckContactExistenceRequest, ExistenceResponse>("CheckContactExistence", request);
        }
    }
}