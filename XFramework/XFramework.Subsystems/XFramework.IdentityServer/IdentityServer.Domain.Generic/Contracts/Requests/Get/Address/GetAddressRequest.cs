using XFramework.Domain.Generic.Contracts.Requests;

namespace IdentityServer.Domain.Generic.Contracts.Requests.Get.Address;

public class GetAddressRequest : RequestBase
{
    public Guid? Guid { get; set; }
}