using XFramework.Domain.Generic.Contracts.Requests;
using XFramework.Domain.Generic.Enums;

namespace IdentityServer.Domain.Generic.Contracts.Requests.Check;

public class CheckContactExistenceRequest : RequestBase
{
    public GenericContactType ContactType { get; set; }
    public string Value { get; set; }
    public Guid? Guid { get; set; }
}