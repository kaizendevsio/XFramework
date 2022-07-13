using XFramework.Domain.Generic.Contracts.Requests;

namespace IdentityServer.Domain.Generic.Contracts.Requests.Update.Location;

public class UpdateLocationRequest : RequestBase
{
    public Guid? AddressEntityGuid { get; set; }
    public Guid? IdentityInfoGuid { get; set; }
}