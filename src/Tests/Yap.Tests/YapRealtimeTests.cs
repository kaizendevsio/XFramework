using System.Text.Json;
using Communications.Domain.Shared.Contracts.Realtime;
using NUnit.Framework;
using Yap.Contracts;
using Yap.Services;

namespace Yap.Tests;

public sealed class YapRealtimeTests
{
    [TestCase("MessageCreated")]
    [TestCase("MessageEdited")]
    [TestCase("ReactionCreated")]
    [TestCase("ReactionDeleted")]
    [TestCase("MessagesRead")]
    [TestCase("MessagesDelivered")]
    public void Frame_TargetedEvent_ExposesOnlyBoundedRoutingMetadata(string kind)
    {
        var id = Guid.NewGuid(); var thread = Guid.NewGuid();
        var frame = YapRealtime.Frame(new CommunicationsRealtimeEvent { ThreadId = thread, EventType = kind,
            PayloadJson = JsonSerializer.Serialize(new { messageId = id, text = "never-forward", secret = "never-forward" }) });
        var hint = JsonSerializer.Deserialize<ChatUpdateHint>(frame[6..].Trim())!;
        Assert.That(hint.ThreadId, Is.EqualTo(thread));
        Assert.That(hint.MessageIds, Is.EqualTo(new[] { id }));
        Assert.That(frame, Does.Not.Contain("never-forward"));
    }

    [TestCase("ThreadMemberRemoved", "{}")]
    [TestCase("MessageDeleted", "{}")]
    [TestCase("MessageCreated", "bad")]
    [TestCase("MessageCreated", "{\"messageId\":12}")]
    [TestCase("MessagesRead", "{\"messageIds\":[]}")]
    public void Frame_UnknownOrMalformedEvent_RequestsReconciliation(string kind, string payload) =>
        Assert.That(YapRealtime.Frame(new() { ThreadId = Guid.NewGuid(), EventType = kind, PayloadJson = payload }), Is.EqualTo("data: refresh\n\n"));

    [Test]
    public void Frame_OversizedReceiptBatch_RequestsReconciliation() =>
        Assert.That(YapRealtime.Frame(new() { ThreadId = Guid.NewGuid(), EventType = "MessagesRead",
            PayloadJson = JsonSerializer.Serialize(new { MessageIds = Enumerable.Range(0, 51).Select(_ => Guid.NewGuid()).ToArray() }) }), Is.EqualTo("data: refresh\n\n"));
}
