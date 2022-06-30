using XFramework.Domain.Generic.Contracts.Requests;
using XFramework.Domain.Generic.Enums;

namespace Messaging.Domin.Generic.Contracts.Requests.Create;

public class CreateVerificationMessageRequest : RequestBase
{
    public string? VerificationToken { get; set; }
    public GenericContactType ContactType { get; set; }
    public string? Contact { get; set; }
}