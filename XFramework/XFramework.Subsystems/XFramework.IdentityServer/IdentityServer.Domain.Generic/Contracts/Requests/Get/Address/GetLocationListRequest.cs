using XFramework.Domain.Generic.Contracts.Requests;

namespace IdentityServer.Domain.Generic.Contracts.Requests.Get.Address;

public class GetLocationListRequest : RequestBase
{
    public Guid? IdentityGuid { get; set; }
}