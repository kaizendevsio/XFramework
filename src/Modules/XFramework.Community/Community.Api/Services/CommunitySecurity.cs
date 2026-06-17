using XFramework.Domain.Shared.DataContext;

namespace Community.Api.Services;

internal static class CommunitySecurity
{
    public static bool IsSpoofed(Guid suppliedId, Guid actualId) =>
        suppliedId != Guid.Empty && suppliedId != actualId;

    public static Result<TOut> ToFailure<TIn, TOut>(Result<TIn> result) =>
        Result<TOut>.Failure(result.Message ?? "Operation failed", result.StatusCode);

    public static Result<CmdResponse>? SaveFailure(DataContextResult result, string operation)
    {
        if (result.IsSuccess)
        {
            return null;
        }

        var statusCode = result.StatusCode >= 400 ? result.StatusCode : 500;
        return Result<CmdResponse>.Failure(
            result.Message ?? $"{operation} failed to save changes",
            statusCode);
    }
}
