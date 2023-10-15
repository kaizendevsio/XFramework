using System.Linq.Expressions;
using System.Text.Json;

namespace XFramework.Integration.Extensions;

public static class HelperExtensions
{
    public static void LoadMapsterDefaults()
    {
        TypeAdapterConfig.GlobalSettings.Default
            .PreserveReference(true)
            .IgnoreNullValues(true);
    }

    public static Expression<Func<TModel, bool>>? DeserializeFilter<TModel>(string filterJson)
    {
        if (string.IsNullOrEmpty(filterJson))
        {
            return null;
        }
        var filter = JsonSerializer.Deserialize<QueryFilter>(filterJson);
        var parameter = Expression.Parameter(typeof(TModel), "x");
        Expression property = Expression.Property(parameter, filter.PropertyName);
        Expression target = Expression.Constant(filter.Value);
        Expression comparisonExpression = null;

        switch (filter.Operation)
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
                throw new InvalidOperationException($"Operation {filter.Operation} is not supported.");
        }

        return Expression.Lambda<Func<TModel, bool>>(comparisonExpression, parameter);
    }
}