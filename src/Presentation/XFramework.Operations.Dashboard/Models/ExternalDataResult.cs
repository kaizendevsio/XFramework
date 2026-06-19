namespace XFramework.Operations.Dashboard.Models;

public sealed record ExternalDataResult<T>(
    bool IsAvailable,
    T Data,
    string? Message)
{
    public static ExternalDataResult<T> Available(T data, string? message = null) =>
        new(true, data, message);

    public static ExternalDataResult<T> Unavailable(T data, string message) =>
        new(false, data, message);
}
