using System.Linq.Expressions;
using XFramework.Domain.Auditing;
namespace Audit.Api.Services;
public static class AuditGridFilters
{
    public static readonly string[] Fields = ["Action", "ActorKind", "Service", "Table"];
    public static readonly string[] Operators = ["Contains", "NotContains", "Equals", "NotEquals", "StartsWith", "EndsWith", "IsEmpty", "IsNotEmpty"];
    public static IQueryable<AuditEvent> Apply(IQueryable<AuditEvent> source, AuditFilter filter)
    {
        var property = filter.Field switch { "Action" => "EventKind", "ActorKind" => "ActorKind", "Service" => "ServiceName", "Table" => "TableName", _ => throw new ArgumentException("Unsupported field") };
        var row = Expression.Parameter(typeof(AuditEvent), "e");
        var member = Expression.Coalesce(Expression.Property(row, property), Expression.Constant(""));
        var value = Expression.Constant(filter.Value ?? "");
        Expression condition = filter.Operator switch
        {
            "Contains" => Expression.Call(member, nameof(string.Contains), null, value),
            "NotContains" => Expression.Not(Expression.Call(member, nameof(string.Contains), null, value)),
            "Equals" => Expression.Equal(member, value),
            "NotEquals" => Expression.NotEqual(member, value),
            "StartsWith" => Expression.Call(member, nameof(string.StartsWith), null, value),
            "EndsWith" => Expression.Call(member, nameof(string.EndsWith), null, value),
            "IsEmpty" => Expression.Equal(member, Expression.Constant("")),
            "IsNotEmpty" => Expression.NotEqual(member, Expression.Constant("")),
            _ => throw new ArgumentException("Unsupported operator")
        };
        return source.Where(Expression.Lambda<Func<AuditEvent, bool>>(condition, row));
    }
}
