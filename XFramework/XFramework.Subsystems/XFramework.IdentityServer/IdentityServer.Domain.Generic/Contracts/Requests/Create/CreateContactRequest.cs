using XFramework.Domain.Generic.Contracts.Requests;
using XFramework.Domain.Generic.Enums;

namespace IdentityServer.Domain.Generic.Contracts.Requests.Create;

public class CreateContactRequest : TransactionRequestBase
{
    public GenericContactType ContactType { get; set; }
    public string Value { get; set; }
    public Guid? CredentialGuid { get; set; }
    public Guid? Guid { get; set; }
    public bool? SendOtp { get; set; }
}