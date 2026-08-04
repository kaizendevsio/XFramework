using System.Buffers;
using XFramework.Core.Patterns;
using XFramework.Core.Services.FeatureGates;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Integration.Attributes;
using XFramework.Integration.Security;

namespace Storage.Api.Features.Sessions.UploadPart;

public static class UploadStorageFilePartEndpoint
{
    private const int MaxUploadPartBytes = 100 * 1024 * 1024;

    [BoltHandler(RequiredServiceScopes = [XFrameworkServiceScopes.StorageWrite])]
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
        IHttpTrustedInvocationAuthorizer invocationAuthorizer,
        ITrustedInvocationFeatureGate featureGate,
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

        var metadata = new RequestMetadata
        {
            RequestId = Guid.NewGuid(),
            OperationName = nameof(UploadStorageFilePartRequest)
        };

        var invocationResult = await invocationAuthorizer.AuthorizeAsync(
            httpRequest.Headers.Authorization.ToString(),
            httpRequest.Headers["X-XFramework-Service-Authorization"].ToString(),
            metadata,
            new InvocationAuthorizationPolicy
            {
                ActorRequirement = ActorRequirement.Required,
                TenantAccessMode = TenantAccessMode.ActorTenant,
                RequireServiceIdentity = false
            },
            ct);
        if (!invocationResult.IsSuccess)
            return TypedResults.Problem(detail: invocationResult.Error, statusCode: invocationResult.StatusCode);

        var featureResult = await featureGate.EnsureAllowedAsync(
            "/api/storage/uploads/sessions/{uploadSessionId:guid}/parts",
            HttpMethods.Post,
            null,
            ct);
        if (!featureResult.IsSuccess)
            return TypedResults.Problem(detail: featureResult.Message, statusCode: featureResult.StatusCode);

        byte[] chunkBytes;
        try
        {
            chunkBytes = await ReadBoundedBodyAsync(httpRequest.Body, ct);
        }
        catch (InvalidDataException)
        {
            return TypedResults.Problem(detail: "Upload part exceeds the 100 MB limit", statusCode: StatusCodes.Status413PayloadTooLarge);
        }

        var request = new UploadStorageFilePartRequest
        {
            Metadata = metadata,
            UploadSessionId = uploadSessionId,
            PartNumber = partNumber,
            OffsetBytes = offsetBytes,
            PartSha256Hash = ReadValue(httpRequest, "partSha256Hash", "X-Storage-Part-Sha256"),
            ChunkBytes = chunkBytes
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

    private static async Task<byte[]> ReadBoundedBodyAsync(Stream body, CancellationToken ct)
    {
        await using var buffer = new MemoryStream();
        var rented = ArrayPool<byte>.Shared.Rent(64 * 1024);
        try
        {
            var totalBytes = 0;
            while (true)
            {
                var bytesRead = await body.ReadAsync(rented.AsMemory(0, rented.Length), ct);
                if (bytesRead == 0)
                    return buffer.ToArray();

                totalBytes += bytesRead;
                if (totalBytes > MaxUploadPartBytes)
                    throw new InvalidDataException("Upload part exceeds the configured limit.");

                await buffer.WriteAsync(rented.AsMemory(0, bytesRead), ct);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }
}
