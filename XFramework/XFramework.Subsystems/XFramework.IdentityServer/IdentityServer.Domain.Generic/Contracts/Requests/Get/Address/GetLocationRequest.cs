using XFramework.Domain.Generic.Contracts.Requests;

namespace IdentityServer.Domain.Generic.Contracts.Requests.Get.Address;

public class GetLocationRequest : RequestBase
{
    public Guid? LocationGuid { get; set; }
}