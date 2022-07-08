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
using Microsoft.AspNetCore.SignalR.Client;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Domain.Generic.Contracts.Responses;
using XFramework.Domain.Generic.Interfaces;

namespace IdentityServer.Integration.Interfaces;

public interface IIdentityServiceWrapper : IXFrameworkService
{
    public HubConnectionState ConnectionState { get; }
        
    public Task<QueryResponse<AuthorizeIdentityResponse>> AuthenticateCredential(AuthenticateCredentialRequest request);
    public Task<QueryResponse<CredentialResponse>> GetCredential(GetCredentialRequest request);
    public Task<QueryResponse<CredentialResponse>> GetCredentialByContact(GetCredentialByContactRequest request);
    public Task<QueryResponse<List<CredentialResponse>>> LegacyGetCredentialList(GetCredentialListRequest request);
    public Task<QueryResponse<List<CredentialResponse>>> GetCredentialList(GetCredentialListRequest request);
    public Task<QueryResponse<ExistenceResponse>> CheckCredentialExistence(CheckCredentialExistenceRequest request);
    public Task<CmdResponse> CreateCredential(CreateCredentialRequest request);
    public Task<CmdResponse> UpdateCredential(UpdateCredentialRequest request);
    public Task<CmdResponse> DeleteCredential(DeleteCredentialRequest request);
    public Task<CmdResponse> ChangePassword(UpdatePasswordRequest request);
    public Task<CmdResponse> SendOneTimePassword(CheckOneTimePasswordRequest request);
        
    public Task<QueryResponse<IdentityRoleResponse>> GetRole(GetRoleRequest request);
    public Task<QueryResponse<List<IdentityRoleResponse>>> GetRoleList(GetRoleListRequest request);
    public Task<QueryResponse<RoleEntityResponse>> GetRoleEntity(GetRoleEntityRequest request);
    public Task<QueryResponse<List<RoleEntityResponse>>> GetRoleEntityList(GetRoleEntityListRequest request);
        
    public Task<QueryResponse<IdentityResponse>> GetIdentity(GetIdentityRequest request);
    public Task<QueryResponse<ExistenceResponse>> CheckIdentityExistence(CheckIdentityExistenceRequest request);
    public Task<CmdResponse> CreateIdentity(CreateIdentityRequest request);
    public Task<CmdResponse> UpdateIdentity(UpdateIdentityRequest request);
    public Task<CmdResponse> DeleteIdentity(DeleteIdentityRequest request);
        
    public Task<QueryResponse<ContactResponse>> GetContact(GetContactRequest request);
    public Task<QueryResponse<List<ContactResponse>>> GetContactList(GetContactListRequest request);
    public Task<QueryResponse<ExistenceResponse>> CheckContactExistence(CheckContactExistenceRequest request);
    public Task<CmdResponse> CreateContact(CreateContactRequest request);
    public Task<CmdResponse> UpdateContact(UpdateContactRequest request);
    public Task<CmdResponse> DeleteContact(DeleteContactRequest request);
    
    public Task<QueryResponse<AddressCountryResponse>> GetAddressEntity(GetAddressEntityRequest request);
    public Task<QueryResponse<List<AddressCountryResponse>>> GetCountryList(GetCountryListRequest request);
    public Task<QueryResponse<List<AddressRegionResponse>>> GetRegionList(GetRegionListRequest request);
    public Task<QueryResponse<List<AddressProvinceResponse>>> GetProvinceList(GetProvinceListRequest request);
    public Task<QueryResponse<List<AddressCityResponse>>> GetCityList(GetCityListRequest request);
    public Task<QueryResponse<List<AddressBarangayResponse>>> GetBarangayList(GetBarangayListRequest request);
    public Task<QueryResponse<IdentityLocationResponse>> GetLocation(GetLocationRequest request);
    public Task<QueryResponse<List<IdentityLocationResponse>>> GetLocationList(GetLocationListRequest request);
    
    public Task<CmdResponse<CreateFileRequest>> CreateFile(CreateFileRequest request);

    public Task<CmdResponse<CreateAddressRequest>> CreateAddress(CreateAddressRequest request);
    public Task<CmdResponse<UpdateAddressRequest>> UpdateAddress(UpdateAddressRequest request);
    public Task<CmdResponse<DeleteAddressRequest>> DeleteAddress(DeleteAddressRequest request);
    
    public Task<QueryResponse<IdentityVerificationSummaryResponse>> CheckVerification(CheckVerificationRequest request);
    public Task<CmdResponse> CreateVerification(CreateVerificationRequest request);
    public Task<CmdResponse> UpdateVerification(UpdateVerificationRequest request);
    public Task<CmdResponse> CreateAffiliateSubscription(CreateAffiliateSubscriptionRequest request);
    public Task<QueryResponse<List<SubscriptionResponse>>> GetUnregisteredSubscriberList(GetUnregisteredSubscriberListRequest request);
}