namespace XFramework.Core.Patterns;

/// <summary>
/// Represents the result of an operation with a value of type T.
/// Provides a consistent way to handle success and failure states across the application.
/// </summary>
/// <typeparam name="T">The type of data returned by the operation</typeparam>
public sealed record Result<T>
{
    /// <summary>
    /// The data returned by the operation (null if operation failed)
    /// </summary>
    public T? Data { get; internal init; }

    /// <summary>
    /// Indicates whether the operation was successful
    /// </summary>
    public bool IsSuccess { get; internal init; }

    /// <summary>
    /// A message describing the result (typically used for errors)
    /// </summary>
    public string? Message { get; internal init; }

    /// <summary>
    /// HTTP status code for the result (200, 404, 400, etc.)
    /// </summary>
    public int StatusCode { get; internal init; }

    /// <summary>
    /// Validation errors, keyed by field name
    /// </summary>
    public IReadOnlyDictionary<string, string[]>? Errors { get; internal init; }

    public static Result<T> Success(T data, string? message = null) => new()
    {
        Data = data,
        IsSuccess = true,
        StatusCode = 200,
        Message = message
    };

    public static Result<T> Success(T data, int statusCode, string? message = null) => new()
    {
        Data = data,
        IsSuccess = true,
        StatusCode = statusCode,
        Message = message
    };

    public static Result<T> Failure(string message, int statusCode = 400) => new()
    {
        IsSuccess = false,
        Message = message,
        StatusCode = statusCode
    };

    public static Result<T> ValidationError(
        Dictionary<string, string[]> errors,
        string? message = "Validation failed") => new()
    {
        IsSuccess = false,
        Message = message,
        StatusCode = 400,
        Errors = errors
    };

    public static Result<T> NotFound(string? message = "Resource not found") => new()
    {
        IsSuccess = false,
        Message = message,
        StatusCode = 404
    };

    public static Result<T> Unauthorized(string? message = "Unauthorized") => new()
    {
        IsSuccess = false,
        Message = message,
        StatusCode = 401
    };

    public static Result<T> Forbidden(string? message = "Forbidden") => new()
    {
        IsSuccess = false,
        Message = message,
        StatusCode = 403
    };

    public static Result<T> Conflict(string? message = "Conflict") => new()
    {
        IsSuccess = false,
        Message = message,
        StatusCode = 409
    };
}

/// <summary>
/// Non-generic Result for operations that don't return data
/// </summary>
public sealed record Result
{
    /// <summary>
    /// Indicates whether the operation was successful
    /// </summary>
    public bool IsSuccess { get; internal init; }

    /// <summary>
    /// A message describing the result
    /// </summary>
    public string? Message { get; internal init; }

    /// <summary>
    /// HTTP status code for the result
    /// </summary>
    public int StatusCode { get; internal init; }

    /// <summary>
    /// Validation errors, keyed by field name
    /// </summary>
    public IReadOnlyDictionary<string, string[]>? Errors { get; internal init; }

    public static Result Success(string? message = null) => new()
    {
        IsSuccess = true,
        StatusCode = 200,
        Message = message
    };

    public static Result Success(int statusCode, string? message = null) => new()
    {
        IsSuccess = true,
        StatusCode = statusCode,
        Message = message
    };

    public static Result Failure(string message, int statusCode = 400) => new()
    {
        IsSuccess = false,
        Message = message,
        StatusCode = statusCode
    };

    public static Result ValidationError(
        Dictionary<string, string[]> errors,
        string? message = "Validation failed") => new()
    {
        IsSuccess = false,
        Message = message,
        StatusCode = 400,
        Errors = errors
    };

    public static Result NotFound(string? message = "Resource not found") => new()
    {
        IsSuccess = false,
        Message = message,
        StatusCode = 404
    };

    public static Result Unauthorized(string? message = "Unauthorized") => new()
    {
        IsSuccess = false,
        Message = message,
        StatusCode = 401
    };

    public static Result Forbidden(string? message = "Forbidden") => new()
    {
        IsSuccess = false,
        Message = message,
        StatusCode = 403
    };

    public static Result Conflict(string? message = "Conflict") => new()
    {
        IsSuccess = false,
        Message = message,
        StatusCode = 409
    };
}
