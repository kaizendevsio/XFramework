using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Enums;

namespace XFramework.Domain.Shared.Extensions;

public static class HelperExtensions
{
    /*example JSON string might look like*/
    /*
     [
         {
            "PropertyName": "Name",
            "Operation": "Contains", // e.g., "Equals", "Contains", etc.
            "Value": "John"
        }
    ]
    */
    
    public static Expression<Func<TModel, bool>>? ToExpression<TModel>(this List<QueryFilter>? filters)
    {
        return DeserializeFilter<TModel>(filters);
    }
    
    public static Expression<Func<TModel, bool>>? ToExpression<TModel>(this string filterJson)
    {
        var filters = JsonSerializer.Deserialize<List<QueryFilter>>(filterJson);
        return DeserializeFilter<TModel>(filters);
    }

    
    public static Expression<Func<TModel, bool>>? DeserializeFilter<TModel>(string filterJson)
    {
        var filters = JsonSerializer.Deserialize<List<QueryFilter>>(filterJson);
        return DeserializeFilter<TModel>(filters);
    }

    public static Expression<Func<TModel, bool>>? DeserializeFilter<TModel>(List<QueryFilter>? filters)
    {
        if (filters is null || !filters.Any())
        {
            return null;
        }

        var parameter = Expression.Parameter(typeof(TModel), "x");
        Expression? combinedExpression = null;

        foreach (var queryFilter in filters)
        {
            // Split the property name to handle nested properties
            string[] propertyNames = queryFilter.PropertyName.Split('.');
            Expression property = parameter;
            Expression? comparisonExpression = null;

            foreach (var name in propertyNames)
            {
                property = Expression.Property(property, name);
            }

            // Adjust for multiple values in the filter
            var values = (queryFilter.Value is IEnumerable enumerable && enumerable?.GetType() != typeof(string))
                ? enumerable.Cast<object>().ToList() // Materialize to list once
                : [queryFilter.Value];

            if (values.Count > 1 && queryFilter.Operation == QueryFilterOperation.Equal) // Handling multiple values
            {
                foreach (var value in values)
                {
                    object convertedValue = value;
                    if (property.Type == typeof(Guid) && value is string stringValue)
                    {
                        convertedValue = Guid.Parse(stringValue);
                    }
                    
                    var target = Expression.Constant(convertedValue, convertedValue.GetType());
                    var equalsExpression = Expression.Equal(property, Expression.Convert(target, property.Type));
                    comparisonExpression = comparisonExpression == null
                        ? equalsExpression
                        : Expression.OrElse(comparisonExpression, equalsExpression);
                }
            }
            else // Original handling for single value cases
            {
                // Handle the conversion for Guids and other types
                Expression target = Expression.Constant(queryFilter.Value, queryFilter.Value?.GetType());
                if (property.Type == typeof(Guid) && queryFilter.Value is string stringValue)
                {
                    MethodInfo guidParseMethod = typeof(Guid).GetMethod(nameof(Guid.Parse), [typeof(string)]);
                    if (guidParseMethod == null) throw new InvalidOperationException("Guid.Parse method not found.");
                    target = Expression.Call(null, guidParseMethod, Expression.Constant(stringValue));
                }
                else if (queryFilter.Value != null)
                {
                    target = Expression.Constant(queryFilter.Value, queryFilter.Value.GetType());
                    target = Expression.Convert(target, property.Type);
                }
                
                switch (queryFilter.Operation)
                {
                    case QueryFilterOperation.Equal:
                        comparisonExpression = Expression.Equal(property, target);
                        break;

                    case QueryFilterOperation.NotEqual:
                        comparisonExpression = Expression.NotEqual(property, target);
                        break;

                    case QueryFilterOperation.Contains when property.Type == typeof(string):
                    {
                        // Assuming 'property' is an Expression representing a string property
                        // and 'target' is an Expression representing the string to search for.

                        // Convert both the property and the target value to lowercase (or uppercase) for a case-insensitive comparison
                        var toLowerMethod = typeof(string).GetMethod("ToLower", Type.EmptyTypes);

                        // Make sure the property expression and the target expression are calling ToLower
                        Expression propertyToLower = Expression.Call(property, toLowerMethod);
                        Expression targetToLower = Expression.Call(target, toLowerMethod);

                        // Now create the Contains call on the lowercase expressions
                        var containsMethod = typeof(string).GetMethod("Contains", [typeof(string)]);
                        comparisonExpression = Expression.Call(propertyToLower, containsMethod, targetToLower);

                        break;
                    }
                    case QueryFilterOperation.GreaterThan:
                        comparisonExpression = Expression.GreaterThan(property, target);
                        break;

                    case QueryFilterOperation.LessThan:
                        comparisonExpression = Expression.LessThan(property, target);
                        break;

                    case QueryFilterOperation.GreaterThanOrEqual:
                        comparisonExpression = Expression.GreaterThanOrEqual(property, target);
                        break;

                    case QueryFilterOperation.LessThanOrEqual:
                        comparisonExpression = Expression.LessThanOrEqual(property, target);
                        break;

                    case QueryFilterOperation.StartsWith when property.Type == typeof(string):
                    {
                        // Convert both the property and the target value to lowercase (or uppercase) for a case-insensitive comparison
                        var toLowerMethod = typeof(string).GetMethod("ToLower", Type.EmptyTypes);

                        // Make sure the property expression and the target expression are calling ToLower
                        Expression propertyToLower = Expression.Call(property, toLowerMethod);
                        Expression targetToLower = Expression.Call(target, toLowerMethod);

                        // Now create the Contains call on the lowercase expressions
                        var containsMethod = typeof(string).GetMethod("StartsWith", [typeof(string)]);
                        comparisonExpression = Expression.Call(propertyToLower, containsMethod, targetToLower);
                        break;
                    }

                    case QueryFilterOperation.EndsWith when property.Type == typeof(string):
                    {
                        // Convert both the property and the target value to lowercase (or uppercase) for a case-insensitive comparison
                        var toLowerMethod = typeof(string).GetMethod("ToLower", Type.EmptyTypes);

                        // Make sure the property expression and the target expression are calling ToLower
                        Expression propertyToLower = Expression.Call(property, toLowerMethod);
                        Expression targetToLower = Expression.Call(target, toLowerMethod);

                        // Now create the Contains call on the lowercase expressions
                        var containsMethod = typeof(string).GetMethod("EndsWith", [typeof(string)]);
                        comparisonExpression = Expression.Call(propertyToLower, containsMethod, targetToLower);
                        break;
                    }

                    // Add more cases as necessary
                    default:
                        throw new InvalidOperationException($"Operation {queryFilter.Operation} is not supported.");
                }
            }

            combinedExpression = combinedExpression == null
                ? comparisonExpression
                : Expression.AndAlso(combinedExpression, comparisonExpression);
        }

        return combinedExpression != null ? Expression.Lambda<Func<TModel, bool>>(combinedExpression, parameter) : null;
    }

    public static string SerializeFilter<TModel>(Expression<Func<TModel, bool>> expression)
    {
        var visitor = new FilterExpressionVisitor();
        var filterConditions = visitor.VisitAndConvert(expression.Body, "");

        return JsonSerializer.Serialize(filterConditions, new JsonSerializerOptions {ReferenceHandler = ReferenceHandler.IgnoreCycles});
    }

    private class FilterExpressionVisitor : ExpressionVisitor
    {
        private List<QueryFilter> Conditions { get; set; } = [];

        protected override Expression VisitBinary(BinaryExpression node)
        {
            var left = node.Left as MemberExpression;
            var right = node.Right as ConstantExpression;

            if (left != null && right != null)
            {
                Conditions.Add(new ()
                {
                    PropertyName = left.Member.Name,
                    Operation = GetOperationName(node.NodeType),
                    Value = right.Value
                });
            }

            return base.VisitBinary(node);
        }

        private QueryFilterOperation GetOperationName(ExpressionType nodeType)
        {
            return nodeType switch
            {
                ExpressionType.Equal => QueryFilterOperation.Equal,
                ExpressionType.NotEqual => QueryFilterOperation.NotEqual,
                ExpressionType.GreaterThan => QueryFilterOperation.GreaterThan,
                ExpressionType.LessThan => QueryFilterOperation.LessThan,
                ExpressionType.GreaterThanOrEqual => QueryFilterOperation.GreaterThanOrEqual,
                ExpressionType.LessThanOrEqual => QueryFilterOperation.LessThanOrEqual,
                ExpressionType.AndAlso => QueryFilterOperation.And,
                ExpressionType.OrElse => QueryFilterOperation.Or,
                // More cases as necessary
                _ => throw new NotSupportedException($"Unsupported operation '{nodeType}'")
            };
        }
        
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.DeclaringType == typeof(string))
            {
                var operation = node.Method.Name switch
                {
                    "Contains" => QueryFilterOperation.Contains,
                    "StartsWith" => QueryFilterOperation.StartsWith,
                    "EndsWith" => QueryFilterOperation.EndsWith,
                    _ => throw new NotSupportedException($"Unsupported method call '{node.Method.Name}'")
                };

                Conditions.Add(new()
                {
                    PropertyName = GetMemberName(node.Object),
                    Operation = operation,
                    Value = GetConstantValue(node.Arguments[0])
                });
            }

            return node;
        }
        
        
        private static string GetMemberName(Expression expression)
        {
            if (expression is MemberExpression memberExpression)
                return memberExpression.Member.Name;

            throw new InvalidOperationException("Expression is not a member access");
        }

        private static object GetConstantValue(Expression expression)
        {
            if (expression is ConstantExpression constantExpression)
                return constantExpression.Value;

            throw new InvalidOperationException("Expression is not a constant");
        }
    }

    // Usage of FilterExpressionVisitor within SerializeFilter
    
    public static string GetTypeFullName(this Type type)
    {
        if (type.IsGenericParameter)
        {
            return type.Name;
        }

        if (!type.IsGenericType)
        {
            return type.FullName ?? type.Name;
        }

        var nameSpan = type.Name.AsSpan();
        var index = nameSpan.IndexOf('`');
        var prefix = index == -1 ? nameSpan : nameSpan.Slice(0, index);

        var builder = new StringBuilder();
        builder.Append(prefix).Append('<');
        var first = true;
        foreach (var arg in type.GetGenericArguments())
        {
            if (!first)
            {
                builder.Append(',');
            }

            var argName = arg.Name;  // Start with simple name

            // Check if arg's FullName contains namespaces, and if so, just use the simple name.
            if (arg.FullName != null && arg.FullName.Contains('.'))
            {
                builder.Append(argName);
            }
            else
            {
                builder.Append(GetTypeFullName(arg));
            }

            first = false;
        }
        builder.Append('>');

        var friendlyName = builder.ToString();
        return friendlyName;
    }
}