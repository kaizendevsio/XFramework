using Bolt.Client;
using FluentAssertions;
using NUnit.Framework;

namespace Bolt.Tests;

[TestFixture]
public sealed class BoltClientPublicApiCompatibilityTests
{
    [Test]
    public void PubSubRemovalMethods_PreserveOriginalClrSignatures()
    {
        typeof(BoltClient).GetMethod(
                nameof(BoltClient.UnregisterDurableSubscriptionAsync),
                [typeof(string), typeof(string), typeof(CancellationToken)])
            .Should().NotBeNull();
        typeof(BoltClient).GetMethod(
                nameof(BoltClient.UnsubscribeAsync),
                [typeof(string), typeof(CancellationToken)])
            .Should().NotBeNull();
    }

    [Test]
    public void ActorAwarePubSubRemovalMethods_HaveDistinctNames()
    {
        typeof(BoltClient).GetMethod(
                nameof(BoltClient.UnregisterDurableSubscriptionWithActorAsync),
                [typeof(string), typeof(string), typeof(string), typeof(CancellationToken)])
            .Should().NotBeNull();
        typeof(BoltClient).GetMethod(
                nameof(BoltClient.UnsubscribeWithActorAsync),
                [typeof(string), typeof(string), typeof(CancellationToken)])
            .Should().NotBeNull();
    }
}
