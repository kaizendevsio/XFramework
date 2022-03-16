using XFramework.Domain.Generic.Contracts.Requests;

namespace IdentityServer.Domain.Generic.Contracts.Requests.Get.Address;

public class GetAddressEntityListRequest : RequestBase
{
    public Guid? ApplicationGuid { get; set; }
}