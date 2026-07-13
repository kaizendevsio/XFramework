using System.Net;
using System.Security.Cryptography;
using System.Text;
using Bolt.Client;
using IdentityServer.Domain.Shared.Contracts.Requests;
using IdentityServer.Domain.Shared.Contracts.Responses;
using MemoryPack;
using XFramework.Domain.Shared.BusinessObjects;

namespace XFramework.Bolt.Phase0Synthetics;

public static class IdentityHealthCheckProbe
{
    private const string IdentityServerServiceName = "XFramework.IdentityServer";

    public static string CommandName => nameof(HealthCheckRequest);

    public static async Task InvokeAndValidateAsync(
        BoltClient client,
        SyntheticOptions options,
        CancellationToken ct)
    {
        var request = new HealthCheckRequest
        {
            Metadata = new RequestMetadata
            {
                TenantId = options.TenantId,
                CredentialId = options.CredentialId,
                RequestId = Guid.NewGuid(),
                Name = "Bolt Phase 0 Synthetic",
                DeviceName = options.DeviceId,
                DeviceAgent = "XFramework.Bolt.Phase0Synthetics"
            }
        };
        var payload = MemoryPackSerializer.Serialize(request);
        var (statusCode, data) = await client.InvokeAsync(
            Sha256Hex(IdentityServerServiceName),
            CommandName,
            payload,
            ct);
        if (!IsSuccess(statusCode) || data.IsEmpty)
            throw new SyntheticCheckException("health_transport_response_invalid");

        QueryResponse<HealthCheckResponse>? response;
        try
        {
            response = MemoryPackSerializer.Deserialize<QueryResponse<HealthCheckResponse>>(data.Span);
        }
        catch
        {
            throw new SyntheticCheckException("health_query_response_invalid");
        }

        if (response is not { IsSuccess: true, Response: not null } ||
            !IsSuccess(response.HttpStatusCode) ||
            !string.Equals(response.Response.Status, "ok", StringComparison.OrdinalIgnoreCase))
        {
            throw new SyntheticCheckException("health_query_response_invalid");
        }

        DateTimeOffset serverTimestamp;
        try
        {
            serverTimestamp = DateTimeOffset.FromUnixTimeMilliseconds(response.Response.Timestamp);
        }
        catch (ArgumentOutOfRangeException)
        {
            throw new SyntheticCheckException("health_timestamp_invalid");
        }

        if ((DateTimeOffset.UtcNow - serverTimestamp).Duration() > TimeSpan.FromMinutes(5))
            throw new SyntheticCheckException("health_timestamp_invalid");
    }

    private static bool IsSuccess(HttpStatusCode statusCode) =>
        (int)statusCode is >= 200 and < 300;

    private static string Sha256Hex(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
