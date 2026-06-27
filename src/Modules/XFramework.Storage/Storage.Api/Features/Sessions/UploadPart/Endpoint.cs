using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Storage.Api.Features.Sessions.UploadPart;

public static class UploadStorageFilePartEndpoint
{
    [BoltHandler]
    public static Task<Result<StorageUploadPartResponse>> Handle(
        UploadStorageFilePartRequest request,
        StorageService storageService,
        CancellationToken ct) =>
        storageService.UploadPartAsync(request, ct);

    public static IEndpointRouteBuilder MapUploadStorageFilePartRestEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/storage/uploads/sessions/{uploadSessionId:guid}/parts", RestHandle)
            .Accepts<byte[]>("application/octet-stream")
            .WithTags("Storage")
            .WithSummary("Upload file part")
            .WithDescription("Uploads a binary file part for a resumable upload session. Bolt callers should use this as the primary chunk path.");

        return app;
    }

    private static async Task<IResult> RestHandle(
        Guid uploadSessionId,
        HttpRequest httpRequest,
        StorageService storageService,
        CancellationToken ct)
    {
        if (uploadSessionId == Guid.Empty)
            return TypedResults.Problem(detail: "Upload session ID is required", statusCode: StatusCodes.Status400BadRequest);

        if (!TryReadInt(httpRequest, "partNumber", "X-Storage-Part-Number", out var partNumber) || partNumber <= 0)
            return TypedResults.Problem(detail: "Part number is required", statusCode: StatusCodes.Status400BadRequest);

        if (!TryReadLong(httpRequest, "offsetBytes", "X-Storage-Offset-Bytes", out var offsetBytes) || offsetBytes < 0)
            return TypedResults.Problem(detail: "Part offset is required", statusCode: StatusCodes.Status400BadRequest);

        if (!IsOctetStream(httpRequest.ContentType))
            return TypedResults.Problem(detail: "Upload part REST endpoint requires application/octet-stream", statusCode: StatusCodes.Status415UnsupportedMediaType);

        await using var buffer = new MemoryStream();
        await httpRequest.Body.CopyToAsync(buffer, ct);

        var request = new UploadStorageFilePartRequest
        {
            UploadSessionId = uploadSessionId,
            PartNumber = partNumber,
            OffsetBytes = offsetBytes,
            PartSha256Hash = ReadValue(httpRequest, "partSha256Hash", "X-Storage-Part-Sha256"),
            ChunkBytes = buffer.ToArray()
        };

        var result = await storageService.UploadPartAsync(request, ct);
        return result.IsSuccess
            ? Results.Json(result.Data, statusCode: result.StatusCode)
            : TypedResults.Problem(detail: result.Message, statusCode: result.StatusCode);
    }

    private static bool TryReadInt(HttpRequest request, string queryName, string headerName, out int value) =>
        int.TryParse(ReadValue(request, queryName, headerName), out value);

    private static bool TryReadLong(HttpRequest request, string queryName, string headerName, out long value) =>
        long.TryParse(ReadValue(request, queryName, headerName), out value);

    private static string? ReadValue(HttpRequest request, string queryName, string headerName)
    {
        if (request.Query.TryGetValue(queryName, out var queryValue) && !string.IsNullOrWhiteSpace(queryValue))
            return queryValue.ToString();

        return request.Headers.TryGetValue(headerName, out var headerValue) && !string.IsNullOrWhiteSpace(headerValue)
            ? headerValue.ToString()
            : null;
    }

    private static bool IsOctetStream(string? contentType) =>
        contentType?.StartsWith("application/octet-stream", StringComparison.OrdinalIgnoreCase) == true;
}
