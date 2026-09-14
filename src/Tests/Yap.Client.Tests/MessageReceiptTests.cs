using NUnit.Framework;
using Yap.Client.Services;
using Yap.Contracts;

namespace Yap.Client.Tests;

public sealed class MessageReceiptTests
{
    [Test]
    public void Sending_IsHiddenUntilFiveSeconds_EvenOnOlderMessages()
    {
        var now = DateTime.UtcNow;
        var message = new ChatMessage { Mine = true, CreatedAt = now, Delivery = "Queued" };
        Assert.That(MessageReceipts.ShowStatus(message, true, now.AddMilliseconds(4999)), Is.False);
        Assert.That(MessageReceipts.ShowStatus(message, false, now.AddSeconds(5)), Is.True);
        message.Delivery = "Sent";
        Assert.That(MessageReceipts.ShowStatus(message, false, now.AddSeconds(6)), Is.False);
        Assert.That(MessageReceipts.ShowStatus(message, true, now.AddSeconds(6)), Is.True);
    }

    [Test]
    public void NewQueuedMessage_ReplacesSentPosition_ButOlderHistoryDoesNotInventOne()
    {
        var old = new ChatMessage { Id = Guid.NewGuid(), Mine = true, CreatedAt = DateTime.UtcNow };
        Assert.That(MessageReceipts.IsLatest(old, [old]), Is.False);
        old.IsLatestOwnMessage = true;
        var pending = new ChatMessage { Id = Guid.NewGuid(), Mine = true, CreatedAt = old.CreatedAt.AddSeconds(1), Delivery = "Queued" };
        Assert.That(MessageReceipts.IsLatest(old, [old, pending]), Is.False);
        Assert.That(MessageReceipts.IsLatest(pending, [old, pending]), Is.True);
    }

    [Test]
    public void Readers_MoveIndependently_WithoutDuplicatingCachedMarkers()
    {
        var a = new Person(Guid.NewGuid(), "A", "a"); var b = new Person(Guid.NewGuid(), "B", "b");
        var old = new ChatMessage { CreatedAt = DateTime.UtcNow, LatestReaders = [a, b] };
        var last = new ChatMessage { CreatedAt = old.CreatedAt.AddSeconds(1), LatestReaders = [a] };
        Assert.That(MessageReceipts.Readers(old, [old, last]), Is.EqualTo(new[] { b }));
        Assert.That(MessageReceipts.Readers(last, [old, last]), Is.EqualTo(new[] { a }));
    }
}
