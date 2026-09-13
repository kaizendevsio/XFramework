using Bolt.Client;
using Bolt.Client.Transport;
using Bolt.Protocol.Transport;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace Bolt.Tests;

public sealed class BoltTransportDiagnosticTests
{
    [Test]
    public void FailedTransport_DoesNotExposeEndpointCredentialsOrExceptionMessage()
    {
        var negotiator = new BoltTransportNegotiator(NullLogger.Instance);
        var options = new BoltClientOptions
        {
            PreferredTransports = [BoltTransport.WebSocket],
            AccessTokenProvider = _ => throw new InvalidOperationException("secret-token-and-ticket")
        };
        var error = Assert.ThrowsAsync<InvalidOperationException>(() => negotiator.ConnectAsync(
            new Uri("wss://yap.test/socket?ticket=private-ticket#private-fragment"), options, CancellationToken.None));
        Assert.Multiple(() =>
        {
            Assert.That(error!.Message, Does.Contain("wss://yap.test/socket"));
            Assert.That(error.Message, Does.Contain("InvalidOperationException"));
            Assert.That(error.Message, Does.Not.Contain("private"));
            Assert.That(error.Message, Does.Not.Contain("secret"));
        });
    }

    [Test]
    public void CallerCancellation_IsNotReplacedByConnectionFailure()
    {
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        var negotiator = new BoltTransportNegotiator(NullLogger.Instance);
        var options = new BoltClientOptions
        {
            AccessTokenProvider = token => { token.ThrowIfCancellationRequested(); return ValueTask.FromResult<string?>(null); }
        };
        Assert.ThrowsAsync<OperationCanceledException>(() => negotiator.ConnectAsync(new Uri("wss://yap.test/socket"), options, canceled.Token));
    }
}
