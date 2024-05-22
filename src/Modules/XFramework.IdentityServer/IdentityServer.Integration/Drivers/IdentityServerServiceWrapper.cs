using IdentityServer.Domain.Shared.Contracts.Requests;
using IdentityServer.Domain.Shared.Contracts.Responses;
using XFramework.Domain.Shared.BusinessObjects;

namespace IdentityServer.Integration.Drivers;

public partial interface IIdentityServerServiceWrapper
{
    public Task<QueryResponse<AuthenticateIdentityResponse>> AuthenticateIdentity(AuthenticateIdentityRequest request);
    public Task<QueryResponse<CheckVerificationResponse>> CheckVerification(CheckVerificationRequest request);
    public Task<CmdResponse> VerifyPassword(VerifyPasswordRequest request);
    public Task<CmdResponse> ChangePassword(ChangePasswordRequest request);
    public Task<CmdResponse> CreateAffiliateSubscription(CreateAffiliateSubscriptionRequest request);

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
    
    public Task<CmdResponse> VerifyPassword(VerifyPasswordRequest request)
    {
        return SendVoidAsync(request);
    }

    public Task<CmdResponse> ChangePassword(ChangePasswordRequest request)
    {
        return SendVoidAsync(request);
    }

    public Task<CmdResponse> CreateAffiliateSubscription(CreateAffiliateSubscriptionRequest request)
    {
        return SendVoidAsync(request);
    }
}