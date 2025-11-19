namespace XFramework.Core.Patterns;

/// <summary>
/// Represents the result of an operation with a value of type T.
/// Provides a consistent way to handle success and failure states across the application.
/// </summary>
/// <typeparam name="T">The type of data returned by the operation</typeparam>
public record Result<T>
{
    /// <summary>
    /// The data returned by the operation (null if operation failed)
    /// </summary>
    public T? Data { get; init; }
    
    /// <summary>
    /// Indicates whether the operation was successful
    /// </summary>
    public bool IsSuccess { get; init; }
    
    /// <summary>
    /// A message describing the result (typically used for errors)
    /// </summary>
    public string? Message { get; init; }
    
    /// <summary>
    /// HTTP status code for the result (200, 404, 400, etc.)
    /// </summary>
    public int StatusCode { get; init; }
    
    /// <summary>
    /// Validation errors, keyed by field name
    /// </summary>
    public Dictionary<string, string[]>? Errors { get; init; }

    /// <summary>
    /// Creates a successful result with data
    /// </summary>
    /// <param name="data">The data to return</param>
    /// <param name="message">Optional success message</param>
    /// <returns>A successful Result</returns>
    public static Result<T> Success(T data, string? message = null) => new()
    {
        Data = data,
        IsSuccess = true,
        StatusCode = 200,
        Message = message
    };

    /// <summary>
    /// Creates a successful result with custom status code
    /// </summary>
    /// <param name="data">The data to return</param>
    /// <param name="statusCode">HTTP status code (e.g., 201 for Created)</param>
    /// <param name="message">Optional success message</param>
    /// <returns>A successful Result</returns>
    public static Result<T> Success(T data, int statusCode, string? message = null) => new()
    {
        Data = data,
        IsSuccess = true,
        StatusCode = statusCode,
        Message = message
    };

    /// <summary>
    /// Creates a failed result with an error message
    /// </summary>
    /// <param name="message">Error message</param>
    /// <param name="statusCode">HTTP status code (default: 400)</param>
    /// <returns>A failed Result</returns>
    public static Result<T> Failure(string message, int statusCode = 400) => new()
    {
        IsSuccess = false,
        Message = message,
        StatusCode = statusCode
    };

    /// <summary>
    /// Creates a failed result with validation errors
    /// </summary>
    /// <param name="errors">Dictionary of validation errors</param>
    /// <param name="message">Optional error message</param>
    /// <returns>A failed Result with validation errors</returns>
    public static Result<T> ValidationError(
        Dictionary<string, string[]> errors,
        string? message = "Validation failed") => new()
    {
        IsSuccess = false,
        Message = message,
        StatusCode = 400,
        Errors = errors
    };

    /// <summary>
    /// Creates a failed result indicating the resource was not found
    /// </summary>
    /// <param name="message">Not found message</param>
    /// <returns>A failed Result with 404 status</returns>
    public static Result<T> NotFound(string? message = "Resource not found") => new()
    {
        IsSuccess = false,
        Message = message,
        StatusCode = 404
    };

    /// <summary>
    /// Creates a failed result indicating unauthorized access
    /// </summary>
    /// <param name="message">Unauthorized message</param>
    /// <returns>A failed Result with 401 status</returns>
    public static Result<T> Unauthorized(string? message = "Unauthorized") => new()
    {
        IsSuccess = false,
        Message = message,
        StatusCode = 401
    };

    /// <summary>
    /// Creates a failed result indicating forbidden access
    /// </summary>
    /// <param name="message">Forbidden message</param>
    /// <returns>A failed Result with 403 status</returns>
    public static Result<T> Forbidden(string? message = "Forbidden") => new()
    {
        IsSuccess = false,
        Message = message,
        StatusCode = 403
    };

    /// <summary>
    /// Creates a failed result indicating a conflict
    /// </summary>
    /// <param name="message">Conflict message</param>
    /// <returns>A failed Result with 409 status</returns>
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
public record Result
{
    /// <summary>
    /// Indicates whether the operation was successful
    /// </summary>
    public bool IsSuccess { get; init; }
    
    /// <summary>
    /// A message describing the result
    /// </summary>
    public string? Message { get; init; }
    
    /// <summary>
    /// HTTP status code for the result
    /// </summary>
    public int StatusCode { get; init; }
    
    /// <summary>
    /// Validation errors, keyed by field name
    /// </summary>
    public Dictionary<string, string[]>? Errors { get; init; }

    /// <summary>
    /// Creates a successful result
    /// </summary>
    /// <param name="message">Optional success message</param>
    /// <returns>A successful Result</returns>
    public static Result Success(string? message = null) => new()
    {
        IsSuccess = true,
        StatusCode = 200,
        Message = message
    };

    /// <summary>
    /// Creates a failed result
    /// </summary>
    /// <param name="message">Error message</param>
    /// <param name="statusCode">HTTP status code (default: 400)</param>
    /// <returns>A failed Result</returns>
    public static Result Failure(string message, int statusCode = 400) => new()
    {
        IsSuccess = false,
        Message = message,
        StatusCode = statusCode
    };

    /// <summary>
    /// Creates a failed result with validation errors
    /// </summary>
    /// <param name="errors">Dictionary of validation errors</param>
    /// <param name="message">Optional error message</param>
    /// <returns>A failed Result with validation errors</returns>
    public static Result ValidationError(
        Dictionary<string, string[]> errors,
        string? message = "Validation failed") => new()
    {
        IsSuccess = false,
        Message = message,
        StatusCode = 400,
        Errors = errors
    };

    /// <summary>
    /// Creates a failed result indicating the resource was not found
    /// </summary>
    /// <param name="message">Not found message</param>
    /// <returns>A failed Result with 404 status</returns>
    public static Result NotFound(string? message = "Resource not found") => new()
    {
        IsSuccess = false,
        Message = message,
        StatusCode = 404
    };
}