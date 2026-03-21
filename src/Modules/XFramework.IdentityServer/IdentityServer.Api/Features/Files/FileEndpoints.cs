using IdentityServer.Api.Features.Files.Upload;

namespace IdentityServer.Api.Features.Files;

/// <summary>
/// Files feature endpoints aggregator
/// </summary>
public static class FileEndpoints
{
    public static void MapFileEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapUploadFile();
    }
}