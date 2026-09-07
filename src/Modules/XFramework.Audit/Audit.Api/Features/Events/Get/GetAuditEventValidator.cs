using FluentValidation;
namespace Audit.Api.Features.Events.Get;
public sealed class GetAuditEventValidator : AbstractValidator<GetAuditEventRequest>
{
    public GetAuditEventValidator() => RuleFor(x => x.EventId).GreaterThan(0);
}
