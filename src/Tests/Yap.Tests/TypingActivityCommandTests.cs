using Communications.Domain.Shared.Contracts.Realtime;
using Communications.Integration.Clients;
using Moq;
using NUnit.Framework;
using Yap.Contracts;
using Yap.Services;

namespace Yap.Tests;

/// <summary>The browser's "typing" action carries an attachment kind and count to the module's typing channel.</summary>
public sealed class TypingActivityCommandTests
{
    private static readonly Guid Thread = Guid.NewGuid();

    [Test]
    public async Task Typing_StaysOnTheOriginalCall_AndAttachmentsCarryKindAndCount()
    {
        var session = new Mock<ICommunicationsChatSession>();
        await YapChatCommands.TypingAsync(new ThreadAction(Thread, "typing", true), session.Object, default);
        await YapChatCommands.TypingAsync(new ThreadAction(Thread, "typing", true, ChatActivity.Photo, 3), session.Object, default);
        await YapChatCommands.TypingAsync(new ThreadAction(Thread, "typing", false, ChatActivity.Recording), session.Object, default);
        session.Verify(x => x.PublishTypingAsync(Thread, true, It.IsAny<CancellationToken>()), Times.Once);
        session.Verify(x => x.PublishTypingAsync(Thread, true, CommunicationsTypingActivity.Photo, 3, It.IsAny<CancellationToken>()), Times.Once);
        session.Verify(x => x.PublishTypingAsync(Thread, false, CommunicationsTypingActivity.Recording, 0, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public void UnknownActivity_IsRefused()
    {
        var session = new Mock<ICommunicationsChatSession>();
        var refused = Assert.ThrowsAsync<YapApiException>(() => YapChatCommands.TypingAsync(new ThreadAction(Thread, "typing", true, (ChatActivity)42, 1), session.Object, default));
        Assert.That(refused!.Status, Is.EqualTo(400));
        session.VerifyNoOtherCalls();
    }

    [TestCase(CommunicationsTypingActivity.Video, 500, ChatActivity.Video, 99)]
    [TestCase(CommunicationsTypingActivity.File, 0, ChatActivity.File, 1)]
    [TestCase(CommunicationsTypingActivity.Typing, 7, ChatActivity.Typing, 0)]
    [TestCase((CommunicationsTypingActivity)42, 3, ChatActivity.Typing, 0)]
    public void Projection_ClampsTheCount_AndReadsUnknownKindsAsTyping(CommunicationsTypingActivity kind, int count, ChatActivity expected, int expectedCount)
    {
        var credential = Guid.NewGuid();
        var update = YapChatCommands.Typing(new() { ThreadId = Thread, CredentialId = credential, IsTyping = true, Activity = kind, Count = count });
        Assert.That(update, Is.EqualTo(new TypingUpdate(Thread, credential, true, expected, expectedCount)));
    }
}
