using IdentityServer.Domain.Generic.Contracts.Requests;
using IdentityServer.Domain.Generic.Contracts.Responses;
using XFramework.Domain.Generic.BusinessObjects;

namespace IdentityServer.Integration.Drivers;

public partial interface IIdentityServerServiceWrapper
{
    public Task<QueryResponse<AuthenticateIdentityResponse>> AuthenticateIdentity(AuthenticateIdentityRequest request);
    public Task<QueryResponse<CheckVerificationResponse>> CheckVerification(CheckVerificationRequest request);
    public Task<CmdResponse> ResetPassword(ResetPasswordRequest request);

}

public partial record IdentityServerServiceWrapper
{
    public Task<QueryResponse<AuthenticateIdentityResponse>> AuthenticateIdentity(AuthenticateIdentityRequest request)
    {
        return SendAsync<AuthenticateIdentityRequest, AuthenticateIdentityResponse>(request);
    }

    public Task<QueryResponse<CheckVerificationResponse>> CheckVerification(CheckVerificationRequest request)
    {
        return SendAsync<CheckVerificationRequest, CheckVerificationResponse>(request);
    }

    public Task<CmdResponse> ResetPassword(ResetPasswordRequest request)
    {
        return SendVoidAsync(request);
    }
}