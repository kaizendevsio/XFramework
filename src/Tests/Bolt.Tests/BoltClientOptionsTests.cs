using System.Reflection;
using Bolt.Client;
using Bolt.Protocol.Transport;
using FluentAssertions;
using NUnit.Framework;

namespace Bolt.Tests;

[TestFixture]
public sealed class BoltClientOptionsTests
{
    [Test]
    public void Clone_PreferredTransportsArray_IsIsolatedFromSource()
    {
        var source = new BoltClientOptions
        {
            PreferredTransports = [BoltTransport.Quic, BoltTransport.WebSocket]
        };
        var cloneMethod = typeof(BoltClientOptions).GetMethod(
            "Clone",
            BindingFlags.Instance | BindingFlags.NonPublic);

        cloneMethod.Should().NotBeNull();
        var clone = cloneMethod!.Invoke(source, null).Should().BeOfType<BoltClientOptions>().Which;

        clone.PreferredTransports.Should().NotBeSameAs(source.PreferredTransports);
        clone.PreferredTransports.Should().Equal(source.PreferredTransports);

        clone.PreferredTransports[0] = BoltTransport.WebTransport;
        source.PreferredTransports[1] = BoltTransport.Quic;

        source.PreferredTransports.Should().Equal(BoltTransport.Quic, BoltTransport.Quic);
        clone.PreferredTransports.Should().Equal(BoltTransport.WebTransport, BoltTransport.WebSocket);
    }
}
