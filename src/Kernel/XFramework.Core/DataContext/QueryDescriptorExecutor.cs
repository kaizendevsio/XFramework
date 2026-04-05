using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using XFramework.Domain.Shared.DataContext;

namespace XFramework.Core.DataContext;

public static class QueryDescriptorExecutor
{
    public static async Task<object?> ExecuteAsync(
        DbContext dbContext,
        Type entityType,
        QueryDescriptor descriptor,
        CancellationToken ct = default)
    {
        var method = typeof(QueryDescriptorExecutor)
            .GetMethod(nameof(ExecuteTypedAsync), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(entityType);

        return await (Task<object?>)method.Invoke(null, [dbContext, descriptor, ct])!;
    }

    public static IAsyncEnumerable<object> ExecuteStreamAsync(
        DbContext dbContext,
        Type entityType,
        QueryDescriptor descriptor,
        CancellationToken ct = default)
    {
        var method = typeof(QueryDescriptorExecutor)
            .GetMethod(nameof(ExecuteStreamTypedAsync), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(entityType);

        return (IAsyncEnumerable<object>)method.Invoke(null, [dbContext, descriptor, ct])!;
    }

    private static async Task<object?> ExecuteTypedAsync<T>(
        DbContext dbContext,
        QueryDescriptor descriptor,
        CancellationToken ct) where T : class
    {
        var queryable = BuildQueryable<T>(dbContext, descriptor);

        return descriptor.Mode switch
        {
            QueryExecutionMode.ToList => await queryable.ToListAsync(ct),
            QueryExecutionMode.FirstOrDefault => await queryable.FirstOrDefaultAsync(ct),
            QueryExecutionMode.SingleOrDefault => await queryable.SingleOrDefaultAsync(ct),
            QueryExecutionMode.Count => await queryable.CountAsync(ct),
            QueryExecutionMode.Any => await queryable.AnyAsync(ct),
            QueryExecutionMode.AnyWithPredicate => await ExecuteAnyWithPredicateAsync(queryable, descriptor.PredicateFilters, ct),
            QueryExecutionMode.All => await ExecuteAllAsync(queryable, descriptor.PredicateFilters, ct),
            QueryExecutionMode.Min => await ExecuteAggregateAsync<T>(queryable, descriptor.AggregateProperty!, "Min", ct),
            QueryExecutionMode.Max => await ExecuteAggregateAsync<T>(queryable, descriptor.AggregateProperty!, "Max", ct),
            QueryExecutionMode.MinBy => await ExecuteMinMaxByAsync<T>(queryable, descriptor.AggregateProperty!, "MinBy", ct),
            QueryExecutionMode.MaxBy => await ExecuteMinMaxByAsync<T>(queryable, descriptor.AggregateProperty!, "MaxBy", ct),
            QueryExecutionMode.Sum => await ExecuteSumAsync(queryable, descriptor.AggregateProperty!, ct),
            QueryExecutionMode.Average => await ExecuteAverageAsync(queryable, descriptor.AggregateProperty!, ct),
            QueryExecutionMode.GroupBy => await ExecuteGroupByAsync<T>(queryable, descriptor.GroupByProperty!, ct),
            QueryExecutionMode.Stream => throw new InvalidOperationException("Use ExecuteStreamAsync for streaming queries."),
            _ => throw new NotSupportedException($"Query execution mode '{descriptor.Mode}' is not supported.")
        };
    }

    private static async IAsyncEnumerable<object> ExecuteStreamTypedAsync<T>(
        DbContext dbContext,
        QueryDescriptor descriptor,
        [EnumeratorCancellation] CancellationToken ct) where T : class
    {
        var queryable = BuildQueryable<T>(dbContext, descriptor);

        await foreach (var item in queryable.AsAsyncEnumerable().WithCancellation(ct))
        {
            yield return item;
        }
    }

    private static IQueryable<T> BuildQueryable<T>(DbContext dbContext, QueryDescriptor descriptor) where T : class
    {
        IQueryable<T> queryable = dbContext.Set<T>().AsNoTracking();

        // Apply filters
        if (descriptor.Filters.Count > 0)
        {
            queryable = ApplyFilters(queryable, descriptor.Filters);
        }

        // Apply sorting
        if (descriptor.Sorting.Count > 0)
        {
            queryable = ApplySorting(queryable, descriptor.Sorting);
        }

        // Apply distinct
        if (descriptor.ApplyDistinct)
        {
            queryable = queryable.Distinct();
        }
        else if (descriptor.DistinctByProperty is not null)
        {
            queryable = ApplyDistinctBy(queryable, descriptor.DistinctByProperty);
        }

        // Apply includes
        foreach (var include in descriptor.Includes)
        {
            queryable = queryable.Include(include);
        }

        // Apply pagination
        if (descriptor.Skip.HasValue)
        {
            queryable = queryable.Skip(descriptor.Skip.Value);
        }

        if (descriptor.Take.HasValue)
        {
            queryable = queryable.Take(descriptor.Take.Value);
        }

        return queryable;
    }

    private static IQueryable<T> ApplyFilters<T>(IQueryable<T> queryable, List<QueryFilter> filters) where T : class
    {
        var parameter = Expression.Parameter(typeof(T), "e");
        Expression? combined = null;

        // Track Or groups
        var i = 0;
        while (i < filters.Count)
        {
            var filter = filters[i];

            if (filter.Operation == QueryFilterOperation.Or)
            {
                // This is an Or group marker — the previous N filters form the group
                i++;
                continue;
            }

            if (filter.Operation == QueryFilterOperation.In)
            {
                // Collect all In values for this property
                var inValues = new List<object?>();
                var propertyName = filter.PropertyName;
                while (i < filters.Count
                       && filters[i].Operation == QueryFilterOperation.In
                       && filters[i].PropertyName == propertyName)
                {
                    inValues.Add(filters[i].Value);
                    i++;
                }

                var inExpression = BuildInExpression(parameter, propertyName!, inValues);
                combined = combined is null ? inExpression : Expression.AndAlso(combined, inExpression);
                continue;
            }

            var filterExpression = BuildFilterExpression(parameter, filter);
            if (filterExpression is not null)
            {
                combined = combined is null ? filterExpression : Expression.AndAlso(combined, filterExpression);
            }

            i++;
        }

        if (combined is not null)
        {
            var lambda = Expression.Lambda<Func<T, bool>>(combined, parameter);
            queryable = queryable.Where(lambda);
        }

        return queryable;
    }

    private static Expression? BuildFilterExpression(ParameterExpression parameter, QueryFilter filter)
    {
        if (filter.PropertyName is null) return null;

        var property = BuildPropertyAccess(parameter, filter.PropertyName);

        return filter.Operation switch
        {
            QueryFilterOperation.Equal when filter.Value is null => Expression.Equal(property, Expression.Constant(null, property.Type)),
            QueryFilterOperation.NotEqual when filter.Value is null => Expression.NotEqual(property, Expression.Constant(null, property.Type)),
            QueryFilterOperation.Equal => Expression.Equal(property, ConvertValue(filter.Value, property.Type)),
            QueryFilterOperation.NotEqual => Expression.NotEqual(property, ConvertValue(filter.Value, property.Type)),
            QueryFilterOperation.GreaterThan => Expression.GreaterThan(property, ConvertValue(filter.Value, property.Type)),
            QueryFilterOperation.LessThan => Expression.LessThan(property, ConvertValue(filter.Value, property.Type)),
            QueryFilterOperation.GreaterThanOrEqual => Expression.GreaterThanOrEqual(property, ConvertValue(filter.Value, property.Type)),
            QueryFilterOperation.LessThanOrEqual => Expression.LessThanOrEqual(property, ConvertValue(filter.Value, property.Type)),
            QueryFilterOperation.Contains => BuildStringMethodCall(property, "Contains", filter.Value),
            QueryFilterOperation.StartsWith => BuildStringMethodCall(property, "StartsWith", filter.Value),
            QueryFilterOperation.EndsWith => BuildStringMethodCall(property, "EndsWith", filter.Value),
            QueryFilterOperation.IsType => BuildTypeCheck(parameter, filter.Value?.ToString()),
            QueryFilterOperation.IsNotType => Expression.Not(BuildTypeCheck(parameter, filter.Value?.ToString())),
            _ => null
        };
    }

    private static Expression BuildPropertyAccess(ParameterExpression parameter, string propertyPath)
    {
        Expression current = parameter;
        foreach (var part in propertyPath.Split('.'))
        {
            current = Expression.Property(current, part);
        }
        return current;
    }

    private static Expression ConvertValue(object? value, Type targetType)
    {
        if (value is null)
            return Expression.Constant(null, targetType);

        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;
        var converted = Convert.ChangeType(value, underlyingType);
        return Expression.Constant(converted, targetType);
    }

    private static Expression BuildStringMethodCall(Expression property, string methodName, object? value)
    {
        var method = typeof(string).GetMethod(methodName, [typeof(string)])!;
        return Expression.Call(property, method, Expression.Constant(value?.ToString() ?? string.Empty));
    }

    private static Expression BuildTypeCheck(ParameterExpression parameter, string? typeName)
    {
        if (typeName is null)
            throw new InvalidOperationException("Type name is required for IsType filter.");

        var type = AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType(typeName))
            .FirstOrDefault(t => t is not null);

        if (type is null)
            throw new InvalidOperationException($"Type '{typeName}' not found.");

        return Expression.TypeIs(parameter, type);
    }

    private static Expression BuildInExpression(ParameterExpression parameter, string propertyName, List<object?> values)
    {
        var property = BuildPropertyAccess(parameter, propertyName);
        var underlyingType = Nullable.GetUnderlyingType(property.Type) ?? property.Type;

        // Build: new[] { val1, val2, val3 }.Contains(e.Property)
        var convertedValues = values
            .Where(v => v is not null)
            .Select(v => Convert.ChangeType(v, underlyingType)!)
            .ToArray();

        var arrayType = Array.CreateInstance(underlyingType, convertedValues.Length);
        for (var i = 0; i < convertedValues.Length; i++)
            arrayType.SetValue(convertedValues[i], i);

        var containsMethod = typeof(Enumerable)
            .GetMethods()
            .First(m => m.Name == "Contains" && m.GetParameters().Length == 2)
            .MakeGenericMethod(underlyingType);

        var arrayConstant = Expression.Constant(arrayType);

        // Handle nullable property: need to access .Value for Contains
        var accessExpression = property.Type != underlyingType
            ? Expression.Convert(property, underlyingType)
            : property;

        return Expression.Call(containsMethod, arrayConstant, accessExpression);
    }

    private static IQueryable<T> ApplySorting<T>(IQueryable<T> queryable, List<SortDescriptor> sorting) where T : class
    {
        var parameter = Expression.Parameter(typeof(T), "e");
        IOrderedQueryable<T>? ordered = null;

        foreach (var sort in sorting)
        {
            var property = BuildPropertyAccess(parameter, sort.PropertyName);
            var lambda = Expression.Lambda(property, parameter);

            var methodName = sort.IsSecondary
                ? sort.Descending ? "ThenByDescending" : "ThenBy"
                : sort.Descending ? "OrderByDescending" : "OrderBy";

            var method = typeof(Queryable)
                .GetMethods()
                .First(m => m.Name == methodName && m.GetParameters().Length == 2)
                .MakeGenericMethod(typeof(T), property.Type);

            if (sort.IsSecondary && ordered is not null)
            {
                ordered = (IOrderedQueryable<T>)method.Invoke(null, [ordered, lambda])!;
            }
            else
            {
                ordered = (IOrderedQueryable<T>)method.Invoke(null, [queryable, lambda])!;
            }
        }

        return ordered ?? queryable;
    }

    private static IQueryable<T> ApplyDistinctBy<T>(IQueryable<T> queryable, string propertyName) where T : class
    {
        var parameter = Expression.Parameter(typeof(T), "e");
        var property = BuildPropertyAccess(parameter, propertyName);
        var lambda = Expression.Lambda(property, parameter);

        var method = typeof(Queryable)
            .GetMethods()
            .First(m => m.Name == "DistinctBy" && m.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(T), property.Type);

        return (IQueryable<T>)method.Invoke(null, [queryable, lambda])!;
    }

    private static async Task<bool> ExecuteAnyWithPredicateAsync<T>(
        IQueryable<T> queryable, List<QueryFilter>? predicateFilters, CancellationToken ct) where T : class
    {
        if (predicateFilters is null || predicateFilters.Count == 0)
            return await queryable.AnyAsync(ct);

        var filtered = ApplyFilters(queryable, predicateFilters);
        return await filtered.AnyAsync(ct);
    }

    private static async Task<bool> ExecuteAllAsync<T>(
        IQueryable<T> queryable, List<QueryFilter>? predicateFilters, CancellationToken ct) where T : class
    {
        if (predicateFilters is null || predicateFilters.Count == 0)
            return true;

        // All(predicate) ≡ !Any(!predicate) — but we need the inverse filter
        // Instead, apply the filter and check count matches
        var totalCount = await queryable.CountAsync(ct);
        var filtered = ApplyFilters(queryable, predicateFilters);
        var matchCount = await filtered.CountAsync(ct);
        return totalCount == matchCount;
    }

    private static async Task<object?> ExecuteAggregateAsync<T>(
        IQueryable<T> queryable, string propertyName, string operation, CancellationToken ct) where T : class
    {
        var parameter = Expression.Parameter(typeof(T), "e");
        var property = BuildPropertyAccess(parameter, propertyName);
        var lambda = Expression.Lambda(property, parameter);

        var method = typeof(EntityFrameworkQueryableExtensions)
            .GetMethods()
            .First(m => m.Name == $"{operation}Async"
                        && m.GetParameters().Length == 3
                        && m.GetGenericArguments().Length == 2)
            .MakeGenericMethod(typeof(T), property.Type);

        var task = (Task)method.Invoke(null, [queryable, lambda, ct])!;
        await task;

        return task.GetType().GetProperty("Result")?.GetValue(task);
    }

    private static async Task<object?> ExecuteMinMaxByAsync<T>(
        IQueryable<T> queryable, string propertyName, string operation, CancellationToken ct) where T : class
    {
        var parameter = Expression.Parameter(typeof(T), "e");
        var property = BuildPropertyAccess(parameter, propertyName);
        var lambda = Expression.Lambda(property, parameter);

        var methodName = $"{operation}Async";
        var method = typeof(EntityFrameworkQueryableExtensions)
            .GetMethods()
            .FirstOrDefault(m => m.Name == methodName
                                 && m.GetParameters().Length == 3
                                 && m.GetGenericArguments().Length == 2);

        // MinByAsync/MaxByAsync may not exist in all EF Core versions — fallback to OrderBy + FirstOrDefault
        if (method is null)
        {
            var sortMethod = operation == "MinBy" ? "OrderBy" : "OrderByDescending";
            var sorted = ApplySorting(queryable, [new SortDescriptor
            {
                PropertyName = propertyName,
                Descending = operation == "MaxBy"
            }]);
            return await sorted.FirstOrDefaultAsync(ct);
        }

        var genericMethod = method.MakeGenericMethod(typeof(T), property.Type);
        var task = (Task)genericMethod.Invoke(null, [queryable, lambda, ct])!;
        await task;
        return task.GetType().GetProperty("Result")?.GetValue(task);
    }

    private static async Task<object?> ExecuteSumAsync<T>(
        IQueryable<T> queryable, string propertyName, CancellationToken ct) where T : class
    {
        var parameter = Expression.Parameter(typeof(T), "e");
        var property = BuildPropertyAccess(parameter, propertyName);
        var lambda = Expression.Lambda<Func<T, decimal>>(Expression.Convert(property, typeof(decimal)), parameter);
        return await queryable.SumAsync(lambda, ct);
    }

    private static async Task<object?> ExecuteAverageAsync<T>(
        IQueryable<T> queryable, string propertyName, CancellationToken ct) where T : class
    {
        var parameter = Expression.Parameter(typeof(T), "e");
        var property = BuildPropertyAccess(parameter, propertyName);
        var lambda = Expression.Lambda<Func<T, decimal>>(Expression.Convert(property, typeof(decimal)), parameter);
        return await queryable.AverageAsync(lambda, ct);
    }

    private static async Task<object?> ExecuteGroupByAsync<T>(
        IQueryable<T> queryable, string propertyName, CancellationToken ct) where T : class
    {
        var parameter = Expression.Parameter(typeof(T), "e");
        var property = BuildPropertyAccess(parameter, propertyName);
        var keyLambda = Expression.Lambda(property, parameter);

        // Use reflection to call GroupBy with the correct key type
        var groupByMethod = typeof(Queryable)
            .GetMethods()
            .First(m => m.Name == "GroupBy" && m.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(T), property.Type);

        var grouped = groupByMethod.Invoke(null, [queryable, keyLambda]);

        // Build Select(g => new GroupResult { Key = g.Key, Items = g.ToList() })
        var groupResultType = typeof(GroupResult<,>).MakeGenericType(property.Type, typeof(T));
        var gParam = Expression.Parameter(typeof(IGrouping<,>).MakeGenericType(property.Type, typeof(T)), "g");

        var keyProp = Expression.Property(gParam, "Key");
        var toListMethod = typeof(Enumerable).GetMethods()
            .First(m => m.Name == "ToList" && m.GetParameters().Length == 1)
            .MakeGenericMethod(typeof(T));
        var toListCall = Expression.Call(toListMethod, gParam);

        var bindings = new List<MemberBinding>
        {
            Expression.Bind(groupResultType.GetProperty("Key")!, keyProp),
            Expression.Bind(groupResultType.GetProperty("Items")!, toListCall)
        };

        var selectBody = Expression.MemberInit(Expression.New(groupResultType), bindings);
        var groupingType = typeof(IGrouping<,>).MakeGenericType(property.Type, typeof(T));
        var selectLambda = Expression.Lambda(selectBody, gParam);

        var selectMethod = typeof(Queryable)
            .GetMethods()
            .First(m => m.Name == "Select" && m.GetParameters().Length == 2
                        && m.GetParameters()[1].ParameterType.GetGenericArguments()[0].GetGenericArguments().Length == 2)
            .MakeGenericMethod(groupingType, groupResultType);

        var selected = selectMethod.Invoke(null, [grouped, selectLambda]);

        // Call ToListAsync on the result
        var toListAsyncMethod = typeof(EntityFrameworkQueryableExtensions)
            .GetMethods()
            .First(m => m.Name == "ToListAsync" && m.GetParameters().Length == 2)
            .MakeGenericMethod(groupResultType);

        var task = (Task)toListAsyncMethod.Invoke(null, [selected, ct])!;
        await task;
        return task.GetType().GetProperty("Result")?.GetValue(task);
    }
}
