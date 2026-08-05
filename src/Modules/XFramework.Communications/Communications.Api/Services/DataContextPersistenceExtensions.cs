using XFramework.Domain.Shared.DataContext;

namespace Communications.Api.Services;

internal static class DataContextPersistenceExtensions
{
    public static async Task SaveChangesOrThrowAsync(
        this IDataContext dataContext,
        CancellationToken ct = default)
    {
        var result = await dataContext.SaveChangesAsync(ct);
        if (!result.IsSuccess)
        {
            throw new CommunicationsPersistenceException(
                result.Message ?? "The Communications database update could not be completed.",
                result.StatusCode);
        }
    }
}

internal sealed class CommunicationsPersistenceException(string message, int statusCode)
    : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
