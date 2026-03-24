using System.Net;

namespace Bolt.Domain.Shared.Contracts.Responses;

/// <summary>
/// Lightweight struct for returning RPC results from InvokeAsync.
/// Replaces the full BoltMessage (15 fields, class allocation)
/// with just the 4 fields we actually need on the response path.
/// </summary>
public readonly struct BoltRpcResult
{
    public HttpStatusCode StatusCode { get; init; }
    public ReadOnlyMemory<byte> Data { get; init; }
    public string? Message { get; init; }
    public TimeSpan Duration { get; init; }

    public bool IsSuccess => (int)StatusCode < 300;
}
