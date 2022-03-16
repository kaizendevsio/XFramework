using XFramework.Domain.Generic.Contracts.Requests;

namespace IdentityServer.Domain.Generic.Contracts.Requests.Get.Address;

public class GetAddressListRequest : RequestBase
{
    public Guid? CredentialGuid { get; set; }
}