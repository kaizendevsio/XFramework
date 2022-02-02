

namespace XFramework.Client.Shared.Core.Wrappers.Interfaces
{
    public interface IIdentityServerWrapper
    {
        public Task<QueryResponseBO<List<IdentityCredentialContract>>> CredentialList(GetIdentityCredentialListRequest request);
        public Task<QueryResponseBO<AuthorizeIdentityContract>> SignIn(AuthenticateCredentialRequest request);
        public Task<CmdResponseBO> SignUp(CreateUserRequest request);
        public Task<CmdResponseBO> OtpRequest(OtpVm request);
        public Task<CmdResponseBO> ForgotPassword(ForgotPasswordVm request);
        
        public Task<CmdResponseBO> UpdateIdentityProfile(CreateUserRequest request);
        public Task<CmdResponseBO> GetUserProfile();

        public Task<CmdResponseBO> CheckContactExistence(CheckContactExistenceRequest request);
        public Task<CmdResponseBO> CheckIdentityExistence(CheckIdentityExistenceRequest request);
        public Task<CmdResponseBO> CheckCredentialExistence(CheckCredentialExistenceRequest request);

    }
}