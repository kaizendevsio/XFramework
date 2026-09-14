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

    [Test]
    public void LockedAndPendingMessages_DoNotRevealPreviouslyDecryptedText()
    {
        var message = new ChatMessage { Text = "Previously decrypted", EncryptionLocked = true };
        Assert.That(ChatState.MessagePreview(message), Is.EqualTo("Unlock messages in Settings"));
        message.EncryptionPending = true;
        Assert.That(ChatState.MessagePreview(message), Is.EqualTo("Waiting for secure delivery"));
    }
}
