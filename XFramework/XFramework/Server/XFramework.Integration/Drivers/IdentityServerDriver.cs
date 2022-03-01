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

        public async Task<QueryResponse<AuthorizeIdentityResponse>> AuthenticateCredential(AuthenticateCredentialRequest request)
        {
            return await SendAsync<AuthenticateCredentialRequest, AuthorizeIdentityResponse>("Authenticate", request);
        }

        public async Task<QueryResponse<CredentialResponse>> GetCredential(GetCredentialRequest request)
        {
            return await SendAsync<GetCredentialRequest, CredentialResponse>("GetCredential", request);
        }

        public async Task<CmdResponse> CreateCredential(CreateCredentialRequest request)
        {
            return await SendVoidAsync<CreateCredentialRequest, CmdResponse>("CreateCredential", request);
        }

        public async Task<CmdResponse> UpdateCredential(UpdateCredentialRequest request)
        {
            return await SendVoidAsync<UpdateCredentialRequest, CmdResponse>("UpdateCredential", request);
        }

        public async Task<CmdResponse> DeleteCredential(DeleteCredentialRequest request)
        {
            return await SendVoidAsync<DeleteCredentialRequest, CmdResponse>("DeleteCredential", request);
        }

        public async Task<CmdResponse> ChangePassword(UpdatePasswordRequest request)
        {
            return await SendVoidAsync<UpdatePasswordRequest, CmdResponse>("ChangePassword", request);
        }
        
        public async Task<CmdResponse> SendOneTimePassword(CheckOneTimePasswordRequest request)
        {
            return await SendVoidAsync<CheckOneTimePasswordRequest, CmdResponse>("SendOneTimePassword", request);
        }

        public async Task<QueryResponse<RoleResponse>> GetRole(GetRoleRequest request)
        {
            return await SendAsync<GetRoleRequest, RoleResponse>("GetRole", request);
        }

        public async Task<QueryResponse<ExistenceResponse>> CheckCredentialExistence(CheckCredentialExistenceRequest request)
        {
            return await SendAsync<CheckCredentialExistenceRequest, ExistenceResponse>("CheckCredentialExistence", request);
        }

        public async Task<QueryResponse<ExistenceResponse>> CheckIdentityExistence(CheckIdentityExistenceRequest request)
        {
            return await SendAsync<CheckIdentityExistenceRequest, ExistenceResponse>("CheckIdentityExistence", request);
        }

        public async Task<QueryResponse<IdentityResponse>> GetIdentity(GetIdentityRequest request)
        {
            return await SendAsync<GetIdentityRequest, IdentityResponse>("GetIdentity", request);
        }

        public async Task<QueryResponse<List<CredentialResponse>>> LegacyGetCredentialList(GetCredentialListRequest request)
        {
            return await SendAsync<GetCredentialListRequest, List<CredentialResponse>>("LegacyGetCredentialList", request);
        }

        public async Task<QueryResponse<List<CredentialResponse>>> GetCredentialList(GetCredentialListRequest request)
        {
            return await SendAsync<GetCredentialListRequest, List<CredentialResponse>>("GetCredentialList", request);
        }

        public async Task<QueryResponse<List<RoleResponse>>> GetRoleList(GetRoleListRequest request)
        {
            return await SendAsync<GetRoleListRequest, List<RoleResponse>>("GetRoleList", request);
        }

        public async Task<QueryResponse<RoleEntityResponse>> GetRoleEntity(GetRoleEntityRequest request)
        {
            return await SendAsync<GetRoleEntityRequest, RoleEntityResponse>("GetRoleEntity", request);
        }

        public async Task<QueryResponse<List<RoleEntityResponse>>> GetRoleEntityList(GetRoleEntityListRequest request)
        {
            return await SendAsync<GetRoleEntityListRequest, List<RoleEntityResponse>>("GetRoleEntityList", request);
        }

        public async Task<CmdResponse> CreateIdentity(CreateIdentityRequest request)
        {
            return await SendVoidAsync<CreateIdentityRequest, CmdResponse>("CreateIdentity", request);
        }

        public async Task<CmdResponse> UpdateIdentity(UpdateIdentityRequest request)
        {
            return await SendVoidAsync<UpdateIdentityRequest, CmdResponse>("UpdateIdentity", request);
        }

        public async Task<CmdResponse> DeleteIdentity(DeleteIdentityRequest request)
        {
            return await SendVoidAsync<DeleteIdentityRequest, CmdResponse>("DeleteIdentity", request);
        }

        public async Task<QueryResponse<ContactResponse>> GetContact(GetContactRequest request)
        {
            return await SendAsync<GetContactRequest, ContactResponse>("GetContact", request);
        }

        public async Task<QueryResponse<List<ContactResponse>>> GetContactList(GetContactListRequest request)
        {
            return await SendAsync<GetContactListRequest, List<ContactResponse>>("GetContactList", request);
        }

        public async Task<CmdResponse> CreateContact(CreateContactRequest request)
        {
            return await SendVoidAsync<CreateContactRequest, CmdResponse>("CreateContact", request);
        }

        public async Task<CmdResponse> UpdateContact(UpdateContactRequest request)
        {
            return await SendVoidAsync<UpdateContactRequest, CmdResponse>("UpdateContact", request);
        }

        public async Task<CmdResponse> DeleteContact(DeleteContactRequest request)
        {
            return await SendVoidAsync<DeleteContactRequest, CmdResponse>("DeleteContact", request);
        }

        public async Task<QueryResponse<ExistenceResponse>> CheckContactExistence(CheckContactExistenceRequest request)
        {
            return await SendAsync<CheckContactExistenceRequest, ExistenceResponse>("CheckContactExistence", request);
        }
    }
}