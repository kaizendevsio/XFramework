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

        public async Task<QueryResponseBO<IdentityCredentialResponse>> GetCredential(GetCredentialRequest request)
        {
            return await SendAsync<GetCredentialRequest, IdentityCredentialResponse>("GetCredential", request);
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

        public async Task<QueryResponseBO<ExistenceResponse>> CheckCredentialExistence(CheckCredentialExistenceRequest request)
        {
            return await SendAsync<CheckCredentialExistenceRequest, ExistenceResponse>("CheckCredentialExistence", request);
        }

        public async Task<QueryResponseBO<ExistenceResponse>> CheckIdentityExistence(CheckIdentityExistenceRequest request)
        {
            return await SendAsync<CheckIdentityExistenceRequest, ExistenceResponse>("CheckIdentityExistence", request);
        }

        public async Task<QueryResponseBO<IdentityInfoResponse>> GetIdentity(GetIdentityRequest request)
        {
            return await SendAsync<GetIdentityRequest, IdentityInfoResponse>("GetIdentity", request);
        }

        public async Task<QueryResponseBO<List<IdentityCredentialResponse>>> GetCredentialList(GetIdentityCredentialListRequest request)
        {
            return await SendAsync<GetIdentityCredentialListRequest, List<IdentityCredentialResponse>>("GetIdentityCredentialList", request);
        }

        public async Task<QueryResponseBO<List<IdentityCredentialResponse>>> GetIdentityRoleList(GetIdentityRoleListRequest request)
        {
            return await SendAsync<GetIdentityRoleListRequest, List<IdentityCredentialResponse>>("GetIdentityRoleList", request);
        }

        public async Task<QueryResponseBO<List<IdentityRoleEntityResponse>>> GetRoleEntityList(GetRoleEntityListRequest request)
        {
            return await SendAsync<GetRoleEntityListRequest, List<IdentityRoleEntityResponse>>("GetRoleEntityList", request);
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

        public async Task<QueryResponseBO<IdentityContactResponse>> GetContact(GetContactRequest request)
        {
            return await SendAsync<GetContactRequest, IdentityContactResponse>("GetContact", request);
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