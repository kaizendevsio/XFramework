using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.Enums;

namespace Messaging.Domain.Shared.Contracts.Requests.Create;

public record CreateVerificationMessageRequest : RequestBase
{
    public string? VerificationToken { get; set; }
    public GenericContactType ContactType { get; set; }
    public string? Contact { get; set; }
}