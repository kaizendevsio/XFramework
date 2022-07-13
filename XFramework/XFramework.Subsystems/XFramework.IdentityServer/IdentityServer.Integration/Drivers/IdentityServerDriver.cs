using IdentityServer.Domain.Generic.Contracts.Requests.Check;
using IdentityServer.Domain.Generic.Contracts.Requests.Check.Verification;
using IdentityServer.Domain.Generic.Contracts.Requests.Create;
using IdentityServer.Domain.Generic.Contracts.Requests.Create.Address;
using IdentityServer.Domain.Generic.Contracts.Requests.Create.Location;
using IdentityServer.Domain.Generic.Contracts.Requests.Create.Storage;
using IdentityServer.Domain.Generic.Contracts.Requests.Create.Subscription;
using IdentityServer.Domain.Generic.Contracts.Requests.Create.Verification;
using IdentityServer.Domain.Generic.Contracts.Requests.Delete;
using IdentityServer.Domain.Generic.Contracts.Requests.Delete.Address;
using IdentityServer.Domain.Generic.Contracts.Requests.Delete.Location;
using IdentityServer.Domain.Generic.Contracts.Requests.Get;
using IdentityServer.Domain.Generic.Contracts.Requests.Get.Address;
using IdentityServer.Domain.Generic.Contracts.Requests.Get.Location;
using IdentityServer.Domain.Generic.Contracts.Requests.Get.Subscription;
using IdentityServer.Domain.Generic.Contracts.Requests.Update;
using IdentityServer.Domain.Generic.Contracts.Requests.Update.Address;
using IdentityServer.Domain.Generic.Contracts.Requests.Update.Location;
using IdentityServer.Domain.Generic.Contracts.Requests.Update.Verification;
using IdentityServer.Domain.Generic.Contracts.Responses;
using IdentityServer.Domain.Generic.Contracts.Responses.Address;
using IdentityServer.Domain.Generic.Contracts.Responses.Subscription;
using IdentityServer.Domain.Generic.Contracts.Responses.Verification;
using Microsoft.Extensions.Configuration;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Domain.Generic.Contracts.Responses;
using XFramework.Integration.Drivers;
using XFramework.Integration.Interfaces.Wrappers;
using IdentityServer.Integration.Interfaces;

namespace IdentityServer.Integration.Drivers;

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
        return await SendAsync<AuthenticateCredentialRequest, AuthorizeIdentityResponse>(request);
    }

    public async Task<QueryResponse<CredentialResponse>> GetCredential(GetCredentialRequest request)
    {
        return await SendAsync<GetCredentialRequest, CredentialResponse>(request);
    }

    public async Task<QueryResponse<CredentialResponse>> GetCredentialByContact(GetCredentialByContactRequest request)
    {
        return await SendAsync<GetCredentialByContactRequest, CredentialResponse>(request);
    }

    public async Task<CmdResponse> CreateCredential(CreateCredentialRequest request)
    {
        return await SendVoidAsync(request);
    }

    public async Task<CmdResponse> UpdateCredential(UpdateCredentialRequest request)
    {
        return await SendVoidAsync(request);
    }

    public async Task<CmdResponse> DeleteCredential(DeleteCredentialRequest request)
    {
        return await SendVoidAsync(request);
    }

    public async Task<CmdResponse> ChangePassword(UpdatePasswordRequest request)
    {
        return await SendVoidAsync(request);
    }
        
    public async Task<CmdResponse> SendOneTimePassword(CheckOneTimePasswordRequest request)
    {
        return await SendVoidAsync(request);
    }

    public async Task<QueryResponse<IdentityRoleResponse>> GetRole(GetRoleRequest request)
    {
        return await SendAsync<GetRoleRequest, IdentityRoleResponse>(request);
    }

    public async Task<QueryResponse<ExistenceResponse>> CheckCredentialExistence(CheckCredentialExistenceRequest request)
    {
        return await SendAsync<CheckCredentialExistenceRequest, ExistenceResponse>(request);
    }

    public async Task<QueryResponse<ExistenceResponse>> CheckIdentityExistence(CheckIdentityExistenceRequest request)
    {
        return await SendAsync<CheckIdentityExistenceRequest, ExistenceResponse>(request);
    }

    public async Task<QueryResponse<IdentityResponse>> GetIdentity(GetIdentityRequest request)
    {
        return await SendAsync<GetIdentityRequest, IdentityResponse>(request);
    }

    public async Task<QueryResponse<List<CredentialResponse>>> LegacyGetCredentialList(GetCredentialListRequest request)
    {
        return await SendAsync<GetCredentialListRequest, List<CredentialResponse>>(request);
    }

    public async Task<QueryResponse<List<CredentialResponse>>> GetCredentialList(GetCredentialListRequest request)
    {
        return await SendAsync<GetCredentialListRequest, List<CredentialResponse>>(request);
    }

    public async Task<QueryResponse<List<IdentityRoleResponse>>> GetRoleList(GetRoleListRequest request)
    {
        return await SendAsync<GetRoleListRequest, List<IdentityRoleResponse>>(request);
    }

    public async Task<QueryResponse<RoleEntityResponse>> GetRoleEntity(GetRoleEntityRequest request)
    {
        return await SendAsync<GetRoleEntityRequest, RoleEntityResponse>(request);
    }

    public async Task<QueryResponse<List<RoleEntityResponse>>> GetRoleEntityList(GetRoleEntityListRequest request)
    {
        return await SendAsync<GetRoleEntityListRequest, List<RoleEntityResponse>>(request);
    }

    public async Task<CmdResponse> CreateIdentity(CreateIdentityRequest request)
    {
        return await SendVoidAsync(request);
    }

    public async Task<CmdResponse> UpdateIdentity(UpdateIdentityRequest request)
    {
        return await SendVoidAsync(request);
    }

    public async Task<CmdResponse> DeleteIdentity(DeleteIdentityRequest request)
    {
        return await SendVoidAsync(request);
    }

    public async Task<QueryResponse<ContactResponse>> GetContact(GetContactRequest request)
    {
        return await SendAsync<GetContactRequest, ContactResponse>(request);
    }

    public async Task<QueryResponse<List<ContactResponse>>> GetContactList(GetContactListRequest request)
    {
        return await SendAsync<GetContactListRequest, List<ContactResponse>>(request);
    }

    public async Task<CmdResponse> CreateContact(CreateContactRequest request)
    {
        return await SendVoidAsync(request);
    }

    public async Task<CmdResponse> UpdateContact(UpdateContactRequest request)
    {
        return await SendVoidAsync(request);
    }

    public async Task<CmdResponse> DeleteContact(DeleteContactRequest request)
    {
        return await SendVoidAsync(request);
    }

    public async Task<QueryResponse<AddressCountryResponse>> GetAddressEntity(GetAddressEntityRequest request)
    {
        return await SendAsync<GetAddressEntityRequest, AddressCountryResponse>(request);
    }

    public async Task<QueryResponse<List<AddressCountryResponse>>> GetCountryList(GetCountryListRequest request)
    {
        return await SendAsync<GetCountryListRequest, List<AddressCountryResponse>>(request);
    }

    public async Task<QueryResponse<List<AddressRegionResponse>>> GetRegionList(GetRegionListRequest request)
    {
        return await SendAsync<GetRegionListRequest, List<AddressRegionResponse>>(request);
    }

    public async Task<QueryResponse<List<AddressProvinceResponse>>> GetProvinceList(GetProvinceListRequest request)
    {
        return await SendAsync<GetProvinceListRequest, List<AddressProvinceResponse>>(request);
    }

    public async Task<QueryResponse<List<AddressCityResponse>>> GetCityList(GetCityListRequest request)
    {
        return await SendAsync<GetCityListRequest, List<AddressCityResponse>>(request);
    }

    public async Task<QueryResponse<List<AddressBarangayResponse>>> GetBarangayList(GetBarangayListRequest request)
    {
        return await SendAsync<GetBarangayListRequest, List<AddressBarangayResponse>>(request);
    }

    public async Task<QueryResponse<List<AddressCountryResponse>>> GetAddressEntityList(GetCountryListRequest request)
    {
        return await SendAsync<GetCountryListRequest, List<AddressCountryResponse>>(request);
    }

    public async Task<QueryResponse<IdentityLocationResponse>> GetLocation(GetLocationRequest request)
    {
        return await SendAsync<GetLocationRequest, IdentityLocationResponse>(request);
    }

    public async Task<QueryResponse<List<IdentityLocationResponse>>> GetLocationList(GetLocationListRequest request)
    {
        return await SendAsync<GetLocationListRequest, List<IdentityLocationResponse>>(request);
    }

    public async Task<CmdResponse<CreateFileRequest>> CreateFile(CreateFileRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<CreateAddressRequest>> CreateAddress(CreateAddressRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<UpdateAddressRequest>> UpdateAddress(UpdateAddressRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<CmdResponse<DeleteAddressRequest>> DeleteAddress(DeleteAddressRequest request)
    {
        return await SendAsync(request);
    }

    public async Task<QueryResponse<IdentityVerificationSummaryResponse>> CheckVerification(CheckVerificationRequest request)
    {
        return await SendAsync<CheckVerificationRequest, IdentityVerificationSummaryResponse>(request);
    }

    public async Task<CmdResponse> CreateVerification(CreateVerificationRequest request)
    {
        return await SendVoidAsync(request);
    }

    public async Task<CmdResponse> UpdateVerification(UpdateVerificationRequest request)
    {
        return await SendVoidAsync(request);
    }

    public async Task<CmdResponse> CreateAffiliateSubscription(CreateAffiliateSubscriptionRequest request)
    {
        return await SendVoidAsync(request);
    }

    public async Task<QueryResponse<List<SubscriptionResponse>>> GetUnregisteredSubscriberList(GetUnregisteredSubscriberListRequest request)
    {
        return await SendAsync<GetUnregisteredSubscriberListRequest, List<SubscriptionResponse>>(request);
    }

    public async Task<QueryResponse<ExistenceResponse>> CheckContactExistence(CheckContactExistenceRequest request)
    {
        return await SendAsync<CheckContactExistenceRequest, ExistenceResponse>(request);
    }
}