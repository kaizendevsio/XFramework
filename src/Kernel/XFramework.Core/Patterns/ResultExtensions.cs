namespace XFramework.Core.Patterns;

/// <summary>
/// Extension methods for working with Result and Result&lt;T&gt; types
/// </summary>
public static class ResultExtensions
{
    /// <summary>
    /// Maps the data of a successful result to a new type
    /// </summary>
    /// <typeparam name="TSource">Source data type</typeparam>
    /// <typeparam name="TDestination">Destination data type</typeparam>
    /// <param name="result">The source result</param>
    /// <param name="mapper">Function to map the data</param>
    /// <returns>A new Result with mapped data if successful, or the original failure</returns>
    public static Result<TDestination> Map<TSource, TDestination>(
        this Result<TSource> result,
        Func<TSource, TDestination> mapper)
    {
        if (!result.IsSuccess || result.Data == null)
        {
            return Result<TDestination>.Failure(
                result.Message ?? "Operation failed",
                result.StatusCode);
        }

        try
        {
            var mappedData = mapper(result.Data);
            return Result<TDestination>.Success(mappedData, result.StatusCode, result.Message);
        }
        catch (Exception ex)
        {
            return Result<TDestination>.Failure($"Mapping failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Asynchronously maps the data of a successful result to a new type
    /// </summary>
    /// <typeparam name="TSource">Source data type</typeparam>
    /// <typeparam name="TDestination">Destination data type</typeparam>
    /// <param name="result">The source result</param>
    /// <param name="mapper">Async function to map the data</param>
    /// <returns>A new Result with mapped data if successful, or the original failure</returns>
    public static async Task<Result<TDestination>> MapAsync<TSource, TDestination>(
        this Result<TSource> result,
        Func<TSource, Task<TDestination>> mapper)
    {
        if (!result.IsSuccess || result.Data == null)
        {
            return Result<TDestination>.Failure(
                result.Message ?? "Operation failed",
                result.StatusCode);
        }

        try
        {
            var mappedData = await mapper(result.Data);
            return Result<TDestination>.Success(mappedData, result.StatusCode, result.Message);
        }
        catch (Exception ex)
        {
            return Result<TDestination>.Failure($"Mapping failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Executes an action if the result is successful
    /// </summary>
    /// <typeparam name="T">Data type</typeparam>
    /// <param name="result">The result</param>
    /// <param name="action">Action to execute on success</param>
    /// <returns>The original result</returns>
    public static Result<T> OnSuccess<T>(
        this Result<T> result,
        Action<T> action)
    {
        if (result.IsSuccess && result.Data != null)
        {
            action(result.Data);
        }

        return result;
    }

    /// <summary>
    /// Executes an async action if the result is successful
    /// </summary>
    /// <typeparam name="T">Data type</typeparam>
    /// <param name="result">The result</param>
    /// <param name="action">Async action to execute on success</param>
    /// <returns>The original result</returns>
    public static async Task<Result<T>> OnSuccessAsync<T>(
        this Result<T> result,
        Func<T, Task> action)
    {
        if (result.IsSuccess && result.Data != null)
        {
            await action(result.Data);
        }

        return result;
    }

    /// <summary>
    /// Executes an action if the result is a failure
    /// </summary>
    /// <typeparam name="T">Data type</typeparam>
    /// <param name="result">The result</param>
    /// <param name="action">Action to execute on failure</param>
    /// <returns>The original result</returns>
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
    /// Chains another result-returning operation if the current result is successful
    /// </summary>
    /// <typeparam name="TSource">Source data type</typeparam>
    /// <typeparam name="TDestination">Destination data type</typeparam>
    /// <param name="result">The source result</param>
    /// <param name="bind">Function that returns a new Result</param>
    /// <returns>The new Result if successful, or the original failure</returns>
    public static Result<TDestination> Bind<TSource, TDestination>(
        this Result<TSource> result,
        Func<TSource, Result<TDestination>> bind)
    {
        if (!result.IsSuccess || result.Data == null)
        {
            return Result<TDestination>.Failure(
                result.Message ?? "Operation failed",
                result.StatusCode);
        }

        try
        {
            return bind(result.Data);
        }
        catch (Exception ex)
        {
            return Result<TDestination>.Failure($"Bind operation failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Asynchronously chains another result-returning operation if the current result is successful
    /// </summary>
    /// <typeparam name="TSource">Source data type</typeparam>
    /// <typeparam name="TDestination">Destination data type</typeparam>
    /// <param name="result">The source result</param>
    /// <param name="bind">Async function that returns a new Result</param>
    /// <returns>The new Result if successful, or the original failure</returns>
    public static async Task<Result<TDestination>> BindAsync<TSource, TDestination>(
        this Result<TSource> result,
        Func<TSource, Task<Result<TDestination>>> bind)
    {
        if (!result.IsSuccess || result.Data == null)
        {
            return Result<TDestination>.Failure(
                result.Message ?? "Operation failed",
                result.StatusCode);
        }

        try
        {
            return await bind(result.Data);
        }
        catch (Exception ex)
        {
            return Result<TDestination>.Failure($"Bind operation failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Matches the result to one of two functions based on success or failure
    /// </summary>
    /// <typeparam name="T">Data type</typeparam>
    /// <typeparam name="TResult">Return type</typeparam>
    /// <param name="result">The result to match</param>
    /// <param name="onSuccess">Function to execute on success</param>
    /// <param name="onFailure">Function to execute on failure</param>
    /// <returns>The result of the appropriate function</returns>
    public static TResult Match<T, TResult>(
        this Result<T> result,
        Func<T, TResult> onSuccess,
        Func<string?, TResult> onFailure)
    {
        return result.IsSuccess && result.Data != null
            ? onSuccess(result.Data)
            : onFailure(result.Message);
    }

    /// <summary>
    /// Converts a Result&lt;T&gt; to a Result (discards the data)
    /// </summary>
    /// <typeparam name="T">Data type</typeparam>
    /// <param name="result">The result to convert</param>
    /// <returns>A non-generic Result</returns>
    public static Result ToResult<T>(this Result<T> result)
    {
        return result.IsSuccess
            ? Result.Success(result.Message)
            : Result.Failure(result.Message ?? "Operation failed", result.StatusCode);
    }

    /// <summary>
    /// Ensures the result data matches a predicate, or returns a failure
    /// </summary>
    /// <typeparam name="T">Data type</typeparam>
    /// <param name="result">The result to check</param>
    /// <param name="predicate">The predicate to evaluate</param>
    /// <param name="errorMessage">Error message if predicate fails</param>
    /// <returns>The original result if predicate passes, or a failure</returns>
    public static Result<T> Ensure<T>(
        this Result<T> result,
        Func<T, bool> predicate,
        string errorMessage)
    {
        if (!result.IsSuccess || result.Data == null)
        {
            return result;
        }

        return predicate(result.Data)
            ? result
            : Result<T>.Failure(errorMessage);
    }

    /// <summary>
    /// Combines multiple results into a single result containing a list
    /// Returns success only if all results are successful
    /// </summary>
    /// <typeparam name="T">Data type</typeparam>
    /// <param name="results">The results to combine</param>
    /// <returns>A result containing a list of all data, or the first failure</returns>
    public static Result<IEnumerable<T>> Combine<T>(
        this IEnumerable<Result<T>> results)
    {
        var resultList = results.ToList();
        var firstFailure = resultList.FirstOrDefault(r => !r.IsSuccess);

        if (firstFailure != null)
        {
            return Result<IEnumerable<T>>.Failure(
                firstFailure.Message ?? "One or more operations failed",
                firstFailure.StatusCode);
        }

        var data = resultList
            .Where(r => r.Data != null)
            .Select(r => r.Data!);

        return Result<IEnumerable<T>>.Success(data);
    }
}