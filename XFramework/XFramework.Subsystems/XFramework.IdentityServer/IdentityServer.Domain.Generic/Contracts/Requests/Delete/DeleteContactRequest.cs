using XFramework.Domain.Generic.Enums;

namespace IdentityServer.Domain.Generic.Contracts.Requests.Delete;

public class DeleteContactRequest
{
    public GenericContactType ContactType { get; set; }
    public Guid? CredentialGuid { get; set; }
    public Guid? Guid { get; set; }
}