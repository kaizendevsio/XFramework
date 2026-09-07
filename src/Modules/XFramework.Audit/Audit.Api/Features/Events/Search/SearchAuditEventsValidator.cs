using FluentValidation;
using System.Text.Json;
namespace Audit.Api.Features.Events.Search;
public sealed class SearchAuditEventsValidator : AbstractValidator<SearchAuditEventsRequest>
{
    public SearchAuditEventsValidator()
    {
        RuleFor(x => x.Filters).NotNull().Must(x => x.Count <= 8);
        RuleForEach(x => x.Filters).ChildRules(f =>
        {
            f.RuleFor(x => x.Field).Must(Audit.Api.Services.AuditGridFilters.Fields.Contains);
            f.RuleFor(x => x.Operator).Must(Audit.Api.Services.AuditGridFilters.Operators.Contains);
            f.RuleFor(x => x.Value).MaximumLength(200);
        });
        RuleFor(x => x.Count).InclusiveBetween(1, 100);
        RuleFor(x => x.StartIndex).InclusiveBetween(0, 100000);
        RuleFor(x => x.To).GreaterThan(x => x.From);
        RuleFor(x => x).Must(x => x.To - x.From <= TimeSpan.FromDays(90)).WithMessage("Select at most 90 days.");
        RuleFor(x => x.Schema).MaximumLength(128);
        RuleFor(x => x.Table).MaximumLength(128);
        RuleFor(x => x.Service).MaximumLength(200);
        RuleFor(x => x.ChangedField).MaximumLength(128);
        RuleFor(x => x.ActorKind).Must(x => x is null or "" or "User" or "System" or "Service" or "Unknown");
        RuleFor(x => x.Action).Must(x => x is null or "" or "insert" or "update" or "delete" or "soft_delete");
        RuleFor(x => x.RelatedMode).Must(x => x is null or "record" or "transaction" or "correlation");
        RuleFor(x => x.AnchorEventId).GreaterThan(0).When(x => x.AnchorEventId.HasValue);
        RuleFor(x => x.EntityKey).MaximumLength(2048).Must(IsObject).WithMessage("Record key must be a JSON object.");
    }
    private static bool IsObject(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return true;
        try { using var doc = JsonDocument.Parse(text); return doc.RootElement.ValueKind == JsonValueKind.Object; }
        catch (JsonException) { return false; }
    }
}
