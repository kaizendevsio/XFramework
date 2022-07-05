using XFramework.Domain.Generic.Contracts.Requests;

namespace IdentityServer.Domain.Generic.Contracts.Requests.Create.Location;

public class CreateLocationRequest : RequestBase
{
    public Guid? AddressEntityGuid { get; set; }
    public Guid? IdentityInfoGuid { get; set; }
}