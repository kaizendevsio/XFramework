using System.Linq.Expressions;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Enums;

namespace XFramework.Integration.DataContext.ExpressionVisitor;

public class QueryExpressionVisitor
{
    public static List<QueryFilter> Parse<T>(Expression<Func<T, bool>> predicate)
    {
        var filters = new List<QueryFilter>();
        Visit(predicate.Body, filters);
        OptimizeOrToIn(filters);
        return filters;
    }

    private static void Visit(Expression expression, List<QueryFilter> filters)
    {
        switch (expression)
        {
            case BinaryExpression binary:
                VisitBinary(binary, filters);
                break;

            case MethodCallExpression methodCall:
                VisitMethodCall(methodCall, filters);
                break;

            case UnaryExpression { NodeType: ExpressionType.Not } unary:
                VisitNot(unary, filters);
                break;

            case TypeBinaryExpression typeBinary:
                VisitTypeIs(typeBinary, filters);
                break;

            // Handle bare boolean member access: x.IsActive → x.IsActive == true
            case MemberExpression member when member.Type == typeof(bool):
                filters.Add(new QueryFilter
                {
                    PropertyName = MemberAccessParser.GetPropertyPath(member),
                    Operation = QueryFilterOperation.Equal,
                    Value = true
                });
                break;

            default:
                throw new NotSupportedException(
                    $"Expression type '{expression.NodeType}' ({expression.GetType().Name}) is not supported. " +
                    "Supported: comparisons (==, !=, >, <, >=, <=), logical (&&, ||, !), " +
                    "string methods (Contains, StartsWith, EndsWith), type checks (is), and boolean properties.");
        }
    }

    private static void VisitBinary(BinaryExpression binary, List<QueryFilter> filters)
    {
        switch (binary.NodeType)
        {
            case ExpressionType.AndAlso:
                Visit(binary.Left, filters);
                Visit(binary.Right, filters);
                break;

            case ExpressionType.OrElse:
                VisitOr(binary, filters);
                break;

            case ExpressionType.Equal:
            case ExpressionType.NotEqual:
            case ExpressionType.GreaterThan:
            case ExpressionType.LessThan:
            case ExpressionType.GreaterThanOrEqual:
            case ExpressionType.LessThanOrEqual:
                VisitComparison(binary, filters);
                break;

            default:
                throw new NotSupportedException(
                    $"Binary expression '{binary.NodeType}' is not supported.");
        }
    }

    private static void VisitComparison(BinaryExpression binary, List<QueryFilter> filters)
    {
        var (propertyName, value) = ExtractPropertyAndValue(binary.Left, binary.Right);

        var operation = binary.NodeType switch
        {
            ExpressionType.Equal => QueryFilterOperation.Equal,
            ExpressionType.NotEqual => QueryFilterOperation.NotEqual,
            ExpressionType.GreaterThan => QueryFilterOperation.GreaterThan,
            ExpressionType.LessThan => QueryFilterOperation.LessThan,
            ExpressionType.GreaterThanOrEqual => QueryFilterOperation.GreaterThanOrEqual,
            ExpressionType.LessThanOrEqual => QueryFilterOperation.LessThanOrEqual,
            _ => throw new NotSupportedException($"Comparison '{binary.NodeType}' is not supported.")
        };

        filters.Add(new QueryFilter
        {
            PropertyName = propertyName,
            Operation = operation,
            Value = value
        });
    }

    private static void VisitOr(BinaryExpression binary, List<QueryFilter> filters)
    {
        // Collect all branches of the Or
        var orFilters = new List<QueryFilter>();
        CollectOrBranches(binary, orFilters);

        // Wrap with Or operation markers
        foreach (var filter in orFilters)
        {
            filter.Operation = filter.Operation switch
            {
                // Keep the actual comparison but mark as part of Or group
                _ => filter.Operation
            };
        }

        if (orFilters.Count > 0)
        {
            // First filter starts the Or group
            orFilters[0].PropertyName = orFilters[0].PropertyName;
            filters.AddRange(orFilters);

            // Add an Or wrapper that references the group
            filters.Add(new QueryFilter
            {
                Operation = QueryFilterOperation.Or,
                Value = orFilters.Count
            });
        }
    }

    private static void CollectOrBranches(Expression expression, List<QueryFilter> filters)
    {
        if (expression is BinaryExpression { NodeType: ExpressionType.OrElse } orBinary)
        {
            CollectOrBranches(orBinary.Left, filters);
            CollectOrBranches(orBinary.Right, filters);
        }
        else
        {
            Visit(expression, filters);
        }
    }

    private static void VisitMethodCall(MethodCallExpression methodCall, List<QueryFilter> filters)
    {
        if (methodCall.Object is null)
        {
            throw new NotSupportedException(
                $"Static method call '{methodCall.Method.Name}' is not supported. " +
                "Only instance methods (string.Contains, StartsWith, EndsWith) are supported.");
        }

        var propertyName = MemberAccessParser.GetPropertyPath(methodCall.Object);
        var argument = methodCall.Arguments.Count > 0 ? EvaluateExpression(methodCall.Arguments[0]) : null;

        var operation = methodCall.Method.Name switch
        {
            "Contains" when methodCall.Object.Type == typeof(string) => QueryFilterOperation.Contains,
            "StartsWith" when methodCall.Object.Type == typeof(string) => QueryFilterOperation.StartsWith,
            "EndsWith" when methodCall.Object.Type == typeof(string) => QueryFilterOperation.EndsWith,
            _ => throw new NotSupportedException(
                $"Method '{methodCall.Method.DeclaringType?.Name}.{methodCall.Method.Name}' is not supported. " +
                "Only string.Contains, StartsWith, and EndsWith are supported.")
        };

        filters.Add(new QueryFilter
        {
            PropertyName = propertyName,
            Operation = operation,
            Value = argument
        });
    }

    private static void VisitNot(UnaryExpression unary, List<QueryFilter> filters)
    {
        switch (unary.Operand)
        {
            // !x.IsActive → x.IsActive == false
            case MemberExpression member when member.Type == typeof(bool):
                filters.Add(new QueryFilter
                {
                    PropertyName = MemberAccessParser.GetPropertyPath(member),
                    Operation = QueryFilterOperation.Equal,
                    Value = false
                });
                break;

            // !(x.Name == null) → x.Name != null (is not null pattern)
            case BinaryExpression { NodeType: ExpressionType.Equal } binary
                when IsNullConstant(binary.Right) || IsNullConstant(binary.Left):
                var expr = IsNullConstant(binary.Right) ? binary.Left : binary.Right;
                filters.Add(new QueryFilter
                {
                    PropertyName = MemberAccessParser.GetPropertyPath(expr),
                    Operation = QueryFilterOperation.NotEqual,
                    Value = null
                });
                break;

            // !(x is SomeType) → IsNotType
            case TypeBinaryExpression typeBinary:
                filters.Add(new QueryFilter
                {
                    PropertyName = MemberAccessParser.GetPropertyPath(typeBinary.Expression),
                    Operation = QueryFilterOperation.IsNotType,
                    Value = typeBinary.TypeOperand.FullName
                });
                break;

            default:
                throw new NotSupportedException(
                    $"Negation of '{unary.Operand.NodeType}' is not supported.");
        }
    }

    private static void VisitTypeIs(TypeBinaryExpression typeBinary, List<QueryFilter> filters)
    {
        filters.Add(new QueryFilter
        {
            PropertyName = MemberAccessParser.GetPropertyPath(typeBinary.Expression),
            Operation = QueryFilterOperation.IsType,
            Value = typeBinary.TypeOperand.FullName
        });
    }

    private static (string PropertyName, object? Value) ExtractPropertyAndValue(Expression left, Expression right)
    {
        // Try: left is property, right is value
        if (TryExtractProperty(left, out var propName) && TryEvaluate(right, out var rightVal))
            return (propName, rightVal);

        // Try: left is value, right is property (e.g., null == x.Name)
        if (TryExtractProperty(right, out propName) && TryEvaluate(left, out var leftVal))
            return (propName, leftVal);

        throw new NotSupportedException(
            "Comparison must have a property access on one side and a constant/variable on the other. " +
            $"Got: {left.NodeType} vs {right.NodeType}");
    }

    private static bool TryExtractProperty(Expression expression, out string propertyName)
    {
        try
        {
            switch (expression)
            {
                case MemberExpression member when IsParameterAccess(member):
                    propertyName = MemberAccessParser.GetPropertyPath(member);
                    return true;

                case UnaryExpression { NodeType: ExpressionType.Convert } unary:
                    return TryExtractProperty(unary.Operand, out propertyName);

                default:
                    propertyName = string.Empty;
                    return false;
            }
        }
        catch
        {
            propertyName = string.Empty;
            return false;
        }
    }

    private static bool IsParameterAccess(MemberExpression member)
    {
        Expression? current = member;
        while (current is MemberExpression m)
            current = m.Expression;
        return current is ParameterExpression;
    }

    private static bool TryEvaluate(Expression expression, out object? value)
    {
        try
        {
            value = EvaluateExpression(expression);
            return true;
        }
        catch
        {
            value = null;
            return false;
        }
    }

    private static object? EvaluateExpression(Expression expression)
    {
        return expression switch
        {
            ConstantExpression constant => constant.Value,
            UnaryExpression { NodeType: ExpressionType.Convert } unary => EvaluateExpression(unary.Operand),
            _ => Expression.Lambda(expression).Compile().DynamicInvoke()
        };
    }

    private static bool IsNullConstant(Expression expression)
    {
        return expression is ConstantExpression { Value: null };
    }

    /// <summary>
    /// Optimizes chains of Or'd Equal filters on the same property into a single In filter.
    /// e.g., x.Status == A || x.Status == B || x.Status == C → x.Status IN (A, B, C)
    /// </summary>
    private static void OptimizeOrToIn(List<QueryFilter> filters)
    {
        // Find Or wrapper markers
        for (var i = filters.Count - 1; i >= 0; i--)
        {
            if (filters[i].Operation != QueryFilterOperation.Or || filters[i].Value is not int count)
                continue;

            var groupStart = i - count;
            if (groupStart < 0) continue;

            var group = filters.GetRange(groupStart, count);

            // Check if all filters in the group target the same property with Equal operation
            if (group.Count >= 2
                && group.All(f => f.Operation == QueryFilterOperation.Equal)
                && group.Select(f => f.PropertyName).Distinct().Count() == 1)
            {
                var propertyName = group[0].PropertyName;
                var values = group.Select(f => f.Value).ToList();

                // Replace the group + Or marker with a single In filter
                filters.RemoveRange(groupStart, count + 1);
                filters.Insert(groupStart, new QueryFilter
                {
                    PropertyName = propertyName,
                    Operation = QueryFilterOperation.In,
                    // Store the first value; the rest are encoded in InternalValue as a list
                    // For now, use string representation of all values
                    Value = values.First()
                });

                // Store additional values as extra In filters (executor will collect them)
                for (var j = 1; j < values.Count; j++)
                {
                    filters.Insert(groupStart + j, new QueryFilter
                    {
                        PropertyName = propertyName,
                        Operation = QueryFilterOperation.In,
                        Value = values[j]
                    });
                }
            }
        }
    }
}
