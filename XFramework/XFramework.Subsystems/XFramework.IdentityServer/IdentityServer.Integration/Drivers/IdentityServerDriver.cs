using IdentityServer.Domain.Generic.Contracts.Requests.Check;
using IdentityServer.Domain.Generic.Contracts.Requests.Check.Verification;
using IdentityServer.Domain.Generic.Contracts.Requests.Create;
using IdentityServer.Domain.Generic.Contracts.Requests.Create.Verification;
using IdentityServer.Domain.Generic.Contracts.Requests.Delete;
using IdentityServer.Domain.Generic.Contracts.Requests.Get;
using IdentityServer.Domain.Generic.Contracts.Requests.Get.Address;
using IdentityServer.Domain.Generic.Contracts.Requests.Update;
using IdentityServer.Domain.Generic.Contracts.Requests.Update.Verification;
using IdentityServer.Domain.Generic.Contracts.Responses;
using IdentityServer.Domain.Generic.Contracts.Responses.Address;
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
        return await SendAsync<AuthenticateCredentialRequest, AuthorizeIdentityResponse>("Authenticate", request);
    }

    public async Task<QueryResponse<CredentialResponse>> GetCredential(GetCredentialRequest request)
    {
        return await SendAsync<GetCredentialRequest, CredentialResponse>("GetCredential", request);
    }

    public async Task<QueryResponse<CredentialResponse>> GetCredentialByContact(GetCredentialByContactRequest request)
    {
        return await SendAsync<GetCredentialByContactRequest, CredentialResponse>("GetCredentialByContact", request);
    }

    public async Task<CmdResponse> CreateCredential(CreateCredentialRequest request)
    {
        return await SendVoidAsync("CreateCredential", request);
    }

    public async Task<CmdResponse> UpdateCredential(UpdateCredentialRequest request)
    {
        return await SendVoidAsync("UpdateCredential", request);
    }

    public async Task<CmdResponse> DeleteCredential(DeleteCredentialRequest request)
    {
        return await SendVoidAsync("DeleteCredential", request);
    }

    public async Task<CmdResponse> ChangePassword(UpdatePasswordRequest request)
    {
        return await SendVoidAsync("ChangePassword", request);
    }
        
    public async Task<CmdResponse> SendOneTimePassword(CheckOneTimePasswordRequest request)
    {
        return await SendVoidAsync("SendOneTimePassword", request);
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
        return await SendVoidAsync("CreateIdentity", request);
    }

    public async Task<CmdResponse> UpdateIdentity(UpdateIdentityRequest request)
    {
        return await SendVoidAsync("UpdateIdentity", request);
    }

    public async Task<CmdResponse> DeleteIdentity(DeleteIdentityRequest request)
    {
        return await SendVoidAsync("DeleteIdentity", request);
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
        return await SendVoidAsync("CreateContact", request);
    }

    public async Task<CmdResponse> UpdateContact(UpdateContactRequest request)
    {
        return await SendVoidAsync("UpdateContact", request);
    }

    public async Task<CmdResponse> DeleteContact(DeleteContactRequest request)
    {
        return await SendVoidAsync("DeleteContact", request);
    }

    public async Task<QueryResponse<AddressCountryResponse>> GetAddressEntity(GetAddressEntityRequest request)
    {
        return await SendAsync<GetAddressEntityRequest, AddressCountryResponse>("GetAddressEntity", request);
    }

    public async Task<QueryResponse<List<AddressCountryResponse>>> GetCountryList(GetCountryListRequest request)
    {
        return await SendAsync<GetCountryListRequest, List<AddressCountryResponse>>("GetCountryList", request);
    }

    public async Task<QueryResponse<List<AddressRegionResponse>>> GetRegionList(GetRegionListRequest request)
    {
        return await SendAsync<GetRegionListRequest, List<AddressRegionResponse>>("GetRegionList", request);
    }

    public async Task<QueryResponse<List<AddressProvinceResponse>>> GetProvinceList(GetProvinceListRequest request)
    {
        return await SendAsync<GetProvinceListRequest, List<AddressProvinceResponse>>("GetProvinceList", request);
    }

    public async Task<QueryResponse<List<AddressCityResponse>>> GetCityList(GetCityListRequest request)
    {
        return await SendAsync<GetCityListRequest, List<AddressCityResponse>>("GetCityList", request);
    }

    public async Task<QueryResponse<List<AddressBarangayResponse>>> GetBarangayList(GetBarangayListRequest request)
    {
        return await SendAsync<GetBarangayListRequest, List<AddressBarangayResponse>>("GetBarangayList", request);
    }

    public async Task<QueryResponse<List<AddressCountryResponse>>> GetAddressEntityList(GetCountryListRequest request)
    {
        return await SendAsync<GetCountryListRequest, List<AddressCountryResponse>>("GetAddressEntityList", request);
    }

    public async Task<QueryResponse<IdentityLocationResponse>> GetLocation(GetLocationRequest request)
    {
        return await SendAsync<GetLocationRequest, IdentityLocationResponse>("GetLocation", request);
    }

    public async Task<QueryResponse<List<IdentityLocationResponse>>> GetLocationList(GetLocationListRequest request)
    {
        return await SendAsync<GetLocationListRequest, List<IdentityLocationResponse>>("GetLocationList", request);
    }

    public async Task<QueryResponse<IdentityVerificationSummaryResponse>> CheckVerification(CheckVerificationRequest request)
    {
        return await SendAsync<CheckVerificationRequest, IdentityVerificationSummaryResponse>("CheckVerification", request);
    }

    public async Task<CmdResponse> CreateVerification(CreateVerificationRequest request)
    {
        return await SendVoidAsync("CreateVerification", request);
    }

    public async Task<CmdResponse> UpdateVerification(UpdateVerificationRequest request)
    {
        return await SendVoidAsync("UpdateVerification", request);
    }

    public async Task<QueryResponse<ExistenceResponse>> CheckContactExistence(CheckContactExistenceRequest request)
    {
        return await SendAsync<CheckContactExistenceRequest, ExistenceResponse>("CheckContactExistence", request);
    }
}