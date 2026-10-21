using System.Linq.Expressions;
using BlazorBlueprint.Primitives;
using BlazorBlueprint.Primitives.DataGrid;
using BlazorBlueprint.Primitives.Filtering;
using XFramework.Domain.Shared.DataContext;

namespace XFramework.Portal.Features.Inventario.Components;

public static class SupplierHistoryQuery
{
    public static IRemoteQuery<T> Apply<T>(IRemoteQuery<T> query, DataGridRequest request,
        string defaultSort, params string[] fields) where T : class
    {
        var parameter = Expression.Parameter(typeof(T), "row");
        Expression<Func<T, object>> Selector(string field)
        {
            field = fields.FirstOrDefault(value => string.Equals(value, field, StringComparison.OrdinalIgnoreCase))
                ?? throw new NotSupportedException("This history column cannot be filtered or sorted.");
            Expression member = parameter;
            foreach (var part in field.Split('.')) member = Expression.Property(member, part);
            return Expression.Lambda<Func<T, object>>(Expression.Convert(member, typeof(object)), parameter);
        }

        foreach (var pair in request.Filters ?? new Dictionary<string, FilterCondition>())
        {
            var selector = Selector(pair.Key);
            var member = ((UnaryExpression)selector.Body).Operand;
            var condition = pair.Value;
            Expression<Func<T, bool>> predicate;
            if (member.Type == typeof(string))
            {
                if (condition.Value is null && condition.Operator is not (FilterOperator.IsEmpty or FilterOperator.IsNotEmpty)) continue;
                var value = Expression.Constant(Convert.ToString(condition.Value) ?? "");
                var nil = Expression.Constant(null, typeof(string));
                var empty = Expression.Constant("");
                Expression body = condition.Operator switch
                {
                    FilterOperator.Equals => Expression.Equal(member, value),
                    FilterOperator.NotEquals => Expression.NotEqual(member, value),
                    FilterOperator.Contains => Expression.AndAlso(Expression.NotEqual(member, nil),
                        Expression.Call(member, nameof(string.Contains), null, value, Expression.Constant(StringComparison.OrdinalIgnoreCase))),
                    FilterOperator.StartsWith => Expression.AndAlso(Expression.NotEqual(member, nil), Expression.Call(member, nameof(string.StartsWith), null, value)),
                    FilterOperator.EndsWith => Expression.AndAlso(Expression.NotEqual(member, nil), Expression.Call(member, nameof(string.EndsWith), null, value)),
                    FilterOperator.IsEmpty => Expression.OrElse(Expression.Equal(member, nil), Expression.Equal(member, empty)),
                    FilterOperator.IsNotEmpty => Expression.AndAlso(Expression.NotEqual(member, nil), Expression.NotEqual(member, empty)),
                    _ => throw new NotSupportedException("This history filter is not supported. Use contains, equals, starts with, ends with, or empty instead.")
                };
                predicate = Expression.Lambda<Func<T, bool>>(body, parameter);
            }
            else
            {
                predicate = condition.ToExpressionForSelector<T>(selector,
                    (Nullable.GetUnderlyingType(member.Type) ?? member.Type) == typeof(DateTime) ? FilterFieldType.Date : null);
                if (predicate.Body is ConstantExpression { Value: true }) continue;
            }
            query = query.Where(predicate);
        }

        var sorted = false;
        foreach (var sort in request.SortDefinitions ?? [])
        {
            if (sort.Direction == SortDirection.None) continue;
            var selector = Selector(sort.ColumnId);
            query = (sorted, sort.Direction == SortDirection.Descending) switch
            {
                (false, false) => query.OrderBy(selector),
                (false, true) => query.OrderByDescending(selector),
                (true, false) => query.ThenBy(selector),
                _ => query.ThenByDescending(selector)
            };
            sorted = true;
        }
        if (!sorted) query = query.OrderByDescending(Selector(defaultSort));
        return query.ThenBy(Expression.Lambda<Func<T, Guid>>(Expression.Property(parameter, "Id"), parameter));
    }
}
