using System.Net.WebSockets;

namespace Bolt.Protocol.Transport;

/// <summary>
/// IBoltConnection implementation over WebSocket.
/// Thin wrapper — delegates directly to WebSocket.
/// </summary>
public sealed class WebSocketBoltConnection : IBoltConnection
{
    private readonly WebSocket _ws;

    public WebSocketBoltConnection(WebSocket webSocket) => _ws = webSocket;

    public BoltTransport TransportType => BoltTransport.WebSocket;

    public bool SupportsDatagrams => false;

    public bool IsConnected => _ws.State == WebSocketState.Open;

    public ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
        => _ws.SendAsync(data, WebSocketMessageType.Binary, endOfMessage: true, ct);

    public async ValueTask<(int BytesRead, bool EndOfMessage)> ReceiveAsync(Memory<byte> buffer, CancellationToken ct = default)
    {
        var result = await _ws.ReceiveAsync(buffer, ct);
        if (result.MessageType == WebSocketMessageType.Close)
            return (0, true);
        return (result.Count, result.EndOfMessage);
    }

    public ValueTask SendDatagramAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
        => ValueTask.CompletedTask;

    public async ValueTask CloseAsync(CancellationToken ct = default)
    {
        if (_ws.State is WebSocketState.Open or WebSocketState.CloseReceived)
            await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, null, ct);
    }

    public ValueTask DisposeAsync()
    {
        _ws.Dispose();
        return ValueTask.CompletedTask;
    }
}
