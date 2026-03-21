namespace XFramework.Core.Patterns;

/// <summary>
/// Extension methods for working with Result and Result&lt;T&gt; types.
/// These methods propagate failures faithfully — IsSuccess is the sole determinant of success,
/// not whether Data is null. Exceptions from user-provided delegates are not swallowed.
/// </summary>
public static class ResultExtensions
{
    /// <summary>
    /// Maps the data of a successful result to a new type.
    /// If the result is a failure, the failure is propagated (preserving Errors).
    /// </summary>
    public static Result<TDestination> Map<TSource, TDestination>(
        this Result<TSource> result,
        Func<TSource?, TDestination> mapper)
    {
        if (!result.IsSuccess)
        {
            return new()
            {
                IsSuccess = false,
                Message = result.Message,
                StatusCode = result.StatusCode,
                Errors = result.Errors
            };
        }

        var mappedData = mapper(result.Data);
        return Result<TDestination>.Success(mappedData, result.StatusCode, result.Message);
    }

    /// <summary>
    /// Asynchronously maps the data of a successful result to a new type.
    /// </summary>
    public static async Task<Result<TDestination>> MapAsync<TSource, TDestination>(
        this Result<TSource> result,
        Func<TSource?, Task<TDestination>> mapper)
    {
        if (!result.IsSuccess)
        {
            return new()
            {
                IsSuccess = false,
                Message = result.Message,
                StatusCode = result.StatusCode,
                Errors = result.Errors
            };
        }

        var mappedData = await mapper(result.Data);
        return Result<TDestination>.Success(mappedData, result.StatusCode, result.Message);
    }

    /// <summary>
    /// Executes an action if the result is successful
    /// </summary>
    public static Result<T> OnSuccess<T>(
        this Result<T> result,
        Action<T?> action)
    {
        if (result.IsSuccess)
        {
            action(result.Data);
        }

        return result;
    }

    /// <summary>
    /// Executes an async action if the result is successful
    /// </summary>
    public static async Task<Result<T>> OnSuccessAsync<T>(
        this Result<T> result,
        Func<T?, Task> action)
    {
        if (result.IsSuccess)
        {
            await action(result.Data);
        }

        return result;
    }

    /// <summary>
    /// Executes an action if the result is a failure
    /// </summary>
    public static Result<T> OnFailure<T>(
        this Result<T> result,
        Action<string?> action)
    {
        if (!result.IsSuccess)
        {
            action(result.Message);
        }

        return result;
    }

    /// <summary>
    /// Chains another result-returning operation if the current result is successful (flatMap).
    /// Failures are propagated faithfully, preserving Errors.
    /// </summary>
    public static Result<TDestination> Bind<TSource, TDestination>(
        this Result<TSource> result,
        Func<TSource?, Result<TDestination>> bind)
    {
        if (!result.IsSuccess)
        {
            return new()
            {
                IsSuccess = false,
                Message = result.Message,
                StatusCode = result.StatusCode,
                Errors = result.Errors
            };
        }

        return bind(result.Data);
    }

    /// <summary>
    /// Asynchronously chains another result-returning operation if the current result is successful.
    /// </summary>
    public static async Task<Result<TDestination>> BindAsync<TSource, TDestination>(
        this Result<TSource> result,
        Func<TSource?, Task<Result<TDestination>>> bind)
    {
        if (!result.IsSuccess)
        {
            return new()
            {
                IsSuccess = false,
                Message = result.Message,
                StatusCode = result.StatusCode,
                Errors = result.Errors
            };
        }

        return await bind(result.Data);
    }

    /// <summary>
    /// Matches the result to one of two functions based on success or failure
    /// </summary>
    public static TResult Match<T, TResult>(
        this Result<T> result,
        Func<T?, TResult> onSuccess,
        Func<string?, TResult> onFailure)
    {
        return result.IsSuccess
            ? onSuccess(result.Data)
            : onFailure(result.Message);
    }

    /// <summary>
    /// Converts a Result&lt;T&gt; to a non-generic Result (discards the data)
    /// </summary>
    public static Result ToResult<T>(this Result<T> result)
    {
        if (result.IsSuccess)
            return Result.Success(result.StatusCode, result.Message);

        return result.Errors is not null
            ? Result.ValidationError(new Dictionary<string, string[]>(result.Errors), result.Message)
            : Result.Failure(result.Message ?? "Operation failed", result.StatusCode);
    }

    /// <summary>
    /// Ensures the result data matches a predicate, or returns a failure
    /// </summary>
    public static Result<T> Ensure<T>(
        this Result<T> result,
        Func<T?, bool> predicate,
        string errorMessage)
    {
        if (!result.IsSuccess)
        {
            return result;
        }

        return predicate(result.Data)
            ? result
            : Result<T>.Failure(errorMessage);
    }

    /// <summary>
    /// Combines multiple results into a single result containing a list.
    /// Returns success only if all results are successful.
    /// </summary>
    public static Result<IEnumerable<T>> Combine<T>(
        this IEnumerable<Result<T>> results)
    {
        var resultList = results.ToList();
        var firstFailure = resultList.FirstOrDefault(r => !r.IsSuccess);

        if (firstFailure is not null)
        {
            return new()
            {
                IsSuccess = false,
                Message = firstFailure.Message ?? "One or more operations failed",
                StatusCode = firstFailure.StatusCode,
                Errors = firstFailure.Errors
            };
        }

        var data = resultList.Select(r => r.Data!);
        return Result<IEnumerable<T>>.Success(data);
    }
}
