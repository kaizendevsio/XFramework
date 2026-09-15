using NUnit.Framework;
using Yap.Client.Services;
using Yap.Contracts;

namespace Yap.Client.Tests;

public sealed class MessagePreviewTests
{
    [TestCase("image/jpeg", "Photo")]
    [TestCase("video/quicktime", "Video")]
    [TestCase("audio/mp4", "Voice message")]
    [TestCase("application/pdf", "Attachment")]
    public void MediaPreview_UsesFriendlyLabelAndRetainsCaption(string type, string expected)
    {
        var message = new ChatMessage { Text = "upload", Attachments = [new(Guid.NewGuid(), "upload", type, 42)] };
        Assert.That(ChatState.MessagePreview(message), Is.EqualTo(expected));
        message.Text = "A caption";
        Assert.That(ChatState.MessagePreview(message), Is.EqualTo("A caption"));
        message.Text = "";
        message.Attachments.Add(new(Guid.NewGuid(), "second", type, 42));
        Assert.That(ChatState.MessagePreview(message), Is.EqualTo("2 attachments"));
    }

    // A received voice message only ever reached the generic file row because its type
    // never resolved to audio/*, so the recipient could not play it inline.
    [TestCase("Voice message.m4a", "application/octet-stream", "audio/mp4", true)]
    [TestCase("Voice message.ogg", "", "audio/ogg", true)]
    [TestCase("Voice message.webm", "audio/webm;codecs=opus", "audio/webm", true)]
    [TestCase("note.wav", "audio/wav", "audio/wav", true)]
    [TestCase("score.mid", "audio/midi", "audio/midi", false)]
    [TestCase("clip.mov", "application/octet-stream", "video/quicktime", false)]
    public void VoiceAttachments_ResolveToPlayableAudio(string name, string type, string expected, bool inline)
    {
        Assert.That(ChatMedia.ContentType(type, name), Is.EqualTo(expected));
        Assert.That(ChatMedia.IsInlineAudio(type, name), Is.EqualTo(inline));
        var message = new ChatMessage { Attachments = [new(Guid.NewGuid(), name, type, 42)] };
        Assert.That(ChatState.MessagePreview(message), Is.EqualTo(expected.StartsWith("audio/") ? "Voice message" : "Video"));
    }

    [Test]
    public void LockedAndPendingMessages_DoNotRevealPreviouslyDecryptedText()
    {
        var message = new ChatMessage { Text = "Previously decrypted", EncryptionLocked = true };
        Assert.That(ChatState.MessagePreview(message), Is.EqualTo("Unlock messages in Settings"));
        message.EncryptionPending = true;
        Assert.That(ChatState.MessagePreview(message), Is.EqualTo("Waiting for secure delivery"));
    }
}
