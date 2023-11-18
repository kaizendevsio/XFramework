﻿using System.Linq.Expressions;
using System.Text;
using System.Text.Json;
using XFramework.Domain.Generic.BusinessObjects;

namespace XFramework.Integration.Extensions;

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
            Expression property = Expression.Property(parameter, queryFilter.PropertyName);
            Expression target = Expression.Constant(queryFilter.Value);
            Expression? comparisonExpression = null;

            switch (queryFilter.Operation)
            {
                case "Equals":
                    comparisonExpression = Expression.Equal(property, target);
                    break;

                case "Contains":
                    // Assumes the property is a string
                    comparisonExpression = Expression.Call(property,
                        typeof(string).GetMethod("Contains", new[] { typeof(string) }), target);
                    break;

                case "GreaterThan":
                    comparisonExpression = Expression.GreaterThan(property, target);
                    break;

                case "LessThan":
                    comparisonExpression = Expression.LessThan(property, target);
                    break;

                case "GreaterThanOrEqual":
                    comparisonExpression = Expression.GreaterThanOrEqual(property, target);
                    break;

                case "LessThanOrEqual":
                    comparisonExpression = Expression.LessThanOrEqual(property, target);
                    break;

                case "StartsWith":
                    // Assumes the property is a string
                    comparisonExpression = Expression.Call(property,
                        typeof(string).GetMethod("StartsWith", new[] { typeof(string) }), target);
                    break;

                case "EndsWith":
                    // Assumes the property is a string
                    comparisonExpression = Expression.Call(property,
                        typeof(string).GetMethod("EndsWith", new[] { typeof(string) }), target);
                    break;

                // Add more cases as necessary
                default:
                    throw new InvalidOperationException($"Operation {queryFilter.Operation} is not supported.");
            }
            
            combinedExpression = combinedExpression == null ? 
                comparisonExpression : 
                Expression.AndAlso(combinedExpression, comparisonExpression);
        }
        

        return Expression.Lambda<Func<TModel, bool>>(combinedExpression, parameter);
    }
    public static string SerializeFilter<TModel>(Expression<Func<TModel, bool>> expression)
    {
        var visitor = new FilterExpressionVisitor();
        var filterConditions = visitor.VisitAndConvert(expression.Body, "");

        return JsonSerializer.Serialize(filterConditions);
    }

    private class FilterExpressionVisitor : ExpressionVisitor
    {
        private List<QueryFilter> Conditions { get; set; } = new();

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

        private string GetOperationName(ExpressionType nodeType)
        {
            return nodeType switch
            {
                ExpressionType.Equal => "Equals",
                ExpressionType.NotEqual => "NotEquals",
                ExpressionType.GreaterThan => "GreaterThan",
                ExpressionType.LessThan => "LessThan",
                ExpressionType.GreaterThanOrEqual => "GreaterThanOrEqual",
                ExpressionType.LessThanOrEqual => "LessThanOrEqual",
                ExpressionType.AndAlso => "And",
                ExpressionType.OrElse => "Or",
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
                    "Contains" => "Contains",
                    "StartsWith" => "StartsWith",
                    "EndsWith" => "EndsWith",
                    _ => throw new NotSupportedException($"Unsupported method call '{node.Method.Name}'")
                };

                Conditions.Add(new QueryFilter
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