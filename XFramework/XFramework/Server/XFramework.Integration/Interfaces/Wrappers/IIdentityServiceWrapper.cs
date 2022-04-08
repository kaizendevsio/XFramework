
using IdentityServer.Domain.Generic.Contracts.Requests.Get.Address;
using IdentityServer.Domain.Generic.Contracts.Responses.Address;
using Microsoft.AspNetCore.SignalR.Client;

namespace XFramework.Integration.Interfaces.Wrappers;

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
        
    public Task<QueryResponse<RoleResponse>> GetRole(GetRoleRequest request);
    public Task<QueryResponse<List<RoleResponse>>> GetRoleList(GetRoleListRequest request);
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
    public Task<QueryResponse<List<AddressCountryResponse>>> GetAddressEntityList(GetAddressEntityListRequest request);
    public Task<QueryResponse<IdentityLocationResponse>> GetLocation(GetLocationRequest request);
    public Task<QueryResponse<List<IdentityLocationResponse>>> GetLocationList(GetLocationListRequest request);
}