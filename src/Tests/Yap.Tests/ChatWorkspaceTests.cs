using System.Net;
using Communications.Domain.Shared.Contracts.Realtime;
using Communications.Domain.Shared.Contracts.Responses;
using Communications.Integration.Clients;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using XFramework.Domain.Shared.BusinessObjects;
using Yap.Services;

namespace Yap.Tests;

[TestFixture]
public sealed class ChatWorkspaceTests
{
    [TestCase("Bob", "Carol")]
    [TestCase("Carol", "Bob")]
    public async Task DirectConversation_ShowsOtherPersonInListAndHeader(string callerName, string peerName)
    {
        var fixture = new ChatFixture();
        var peer = Guid.NewGuid();
        var directory = new Mock<IChatDirectory>();
        directory.Setup(d => d.ResolveAsync(It.IsAny<Guid[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new ChatPerson(peer, peerName, peerName.ToLowerInvariant()) });
        fixture.Session.Setup(s => s.GetThreadsAsync(0, 30, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ChatFixture.Ok(new GetThreadListResponse { TotalCount = 2, Items =
            [new() { Id = fixture.Thread, Name = "Shared stored title", IsDirect = true, OtherCredentialId = peer },
             new() { Id = Guid.NewGuid(), Name = "Project group", IsDirect = false }] }));
        fixture.Session.Setup(s => s.GetThreadAsync(fixture.Thread, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ChatFixture.Ok(new GetThreadResponse { Id = fixture.Thread, Name = "Shared stored title", IsDirect = true,
                Members = [new() { CredentialId = fixture.Credential, Alias = callerName }, new() { CredentialId = peer }] }));
        await using var workspace = new ChatWorkspace(fixture.Client.Object, directory.Object, NullLogger<ChatWorkspace>.Instance);

        await workspace.StartAsync("device");
        await workspace.SelectAsync(fixture.Thread);

        Assert.That(workspace.Conversations[0].Name, Is.EqualTo(peerName));
        Assert.That(workspace.Selected!.Name, Is.EqualTo(peerName));
        Assert.That(workspace.Conversations[1].Name, Is.EqualTo("Project group"));
        directory.Verify(d => d.ResolveAsync(It.Is<Guid[]>(ids => ids.Length == 1 && ids[0] == peer), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task StartAsync_FailedInitialLoad_CanRetryAndSubscribe()
    {
        var fixture = new ChatFixture();
        fixture.Session.SetupSequence(s => s.GetThreadsAsync(0, 30, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryResponse<GetThreadListResponse> { HttpStatusCode = HttpStatusCode.ServiceUnavailable })
            .ReturnsAsync(ChatFixture.Ok(new GetThreadListResponse()));
        await using var workspace = fixture.Workspace();
        Assert.ThrowsAsync<ChatOperationException>(async () => await workspace.StartAsync("device"));
        Assert.That(workspace.Ready, Is.False);
        await workspace.StartAsync("device");
        Assert.That(workspace.Ready, Is.True);
        fixture.Session.Verify(s => s.SubscribeUserEventsAsync(It.IsAny<Func<CommunicationsRealtimeEvent, Task>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task SendAsync_UnconfirmedFailure_DoesNotRetryOrInventMessage()
    {
        var fixture = new ChatFixture();
        fixture.Session.Setup(s => s.SendMessageAsync(fixture.Thread, "hello", null, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException());
        await using var workspace = fixture.Workspace();
        await workspace.StartAsync("device");
        Assert.ThrowsAsync<TimeoutException>(async () => await workspace.SendAsync(fixture.Thread, "hello"));
        fixture.Session.Verify(s => s.SendMessageAsync(fixture.Thread, "hello", null, null, It.IsAny<CancellationToken>()), Times.Once);
        Assert.That(workspace.Messages, Is.Empty);
    }

    [Test]
    public async Task SendAsync_Reply_PreservesParentAndThread()
    {
        var fixture = new ChatFixture();
        var parent = Guid.NewGuid();
        var message = Guid.NewGuid();
        fixture.Session.Setup(s => s.SendMessageAsync(fixture.Thread, "reply", parent, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ChatFixture.Ok(new CreateThreadMessageResponse { MessageId = message }));
        await using var workspace = fixture.Workspace();
        await workspace.StartAsync("device");
        Assert.That(await workspace.SendAsync(fixture.Thread, " reply ", parent), Is.EqualTo(message));
    }

    [Test]
    public async Task UserEvent_WrongTenant_DoesNotReadAnotherTenantsData()
    {
        var fixture = new ChatFixture();
        await using var workspace = fixture.Workspace();
        await workspace.StartAsync("device");
        await fixture.OnEvent!(new CommunicationsRealtimeEvent { TenantId = Guid.NewGuid() });
        fixture.Session.Verify(s => s.GetThreadsAsync(0, 30, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task UserEvent_RefreshFailure_PropagatesSoWrapperDoesNotAcknowledge()
    {
        var fixture = new ChatFixture();
        await using var workspace = fixture.Workspace();
        await workspace.StartAsync("device");
        fixture.Session.Setup(s => s.GetThreadsAsync(0, 30, It.IsAny<CancellationToken>())).ThrowsAsync(new TimeoutException());
        Assert.ThrowsAsync<TimeoutException>(async () => await fixture.OnEvent!(new CommunicationsRealtimeEvent { TenantId = fixture.Tenant }));
        Assert.That(workspace.LiveError, Is.Not.Null);
    }

    [Test]
    public async Task SelectAsync_LoadedMessages_AreChronologicalAndNotMarkedReadUntilUiConfirms()
    {
        var fixture = new ChatFixture();
        var older = new ThreadMessageItemResponse { Id = Guid.NewGuid(), Text = "older", CreatedAt = DateTime.UtcNow.AddMinutes(-1) };
        var newer = new ThreadMessageItemResponse { Id = Guid.NewGuid(), Text = "newer", CreatedAt = DateTime.UtcNow };
        fixture.Session.Setup(s => s.GetMessagesAsync(fixture.Thread, 0, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ChatFixture.Ok(new GetThreadMessagesResponse { Items = [newer, older, newer], TotalCount = 2 }));
        await using var workspace = fixture.Workspace();
        await workspace.StartAsync("device");
        await workspace.SelectAsync(fixture.Thread);
        Assert.That(workspace.Messages.Select(m => m.Text), Is.EqualTo(new[] { "older", "newer" }));
        fixture.Session.Verify(s => s.MarkReadAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
        await workspace.MarkReadAsync(fixture.Thread, [older.Id, newer.Id]);
        fixture.Session.Verify(s => s.MarkReadAsync(fixture.Thread, It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task DisposeAsync_StartedWorkspace_CancelsSubscription()
    {
        var fixture = new ChatFixture();
        var workspace = fixture.Workspace();
        await workspace.StartAsync("device");
        await workspace.DisposeAsync();
        Assert.That(fixture.SubscriptionToken.IsCancellationRequested, Is.True);
    }

    [Test]
    public async Task SearchAsync_ConversationScope_PreservesScopeAndPage()
    {
        var fixture = new ChatFixture();
        fixture.Session.Setup(s => s.SearchMessagesAsync("older message", fixture.Thread, 2, 30, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ChatFixture.Ok(new SearchMessagesResponse { TotalCount = 61 }));
        await using var workspace = fixture.Workspace();
        await workspace.StartAsync("device");
        var result = await workspace.SearchAsync(" older message ", fixture.Thread, 2);
        Assert.That(result.TotalCount, Is.EqualTo(61));
    }

    [Test]
    public async Task Typing_GroupMemberStops_DoesNotClearOtherMember()
    {
        var fixture = new ChatFixture();
        Func<CommunicationsTypingState, Task>? onTyping = null;
        fixture.Session.Setup(s => s.SubscribeTypingAsync(fixture.Thread, It.IsAny<Func<CommunicationsTypingState, Task>>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, Func<CommunicationsTypingState, Task>, CancellationToken>((_, handler, _) => onTyping = handler)
            .Returns(Task.CompletedTask);
        await using var workspace = fixture.Workspace();
        await workspace.StartAsync("device");
        await workspace.SelectAsync(fixture.Thread);
        var bob = Guid.NewGuid();
        var carol = Guid.NewGuid();
        await onTyping!(new() { ThreadId = fixture.Thread, CredentialId = bob, IsTyping = true });
        await onTyping(new() { ThreadId = fixture.Thread, CredentialId = carol, IsTyping = true });
        await onTyping(new() { ThreadId = fixture.Thread, CredentialId = bob, IsTyping = false });
        Assert.That(workspace.IsTyping, Is.True);
        await onTyping(new() { ThreadId = fixture.Thread, CredentialId = carol, IsTyping = false });
        Assert.That(workspace.IsTyping, Is.False);
    }

    [Test]
    public async Task OpenMessageAsync_OlderSearchResult_LoadsHistoryUntilFound()
    {
        var fixture = new ChatFixture();
        var recent = new ThreadMessageItemResponse { Id = Guid.NewGuid(), Text = "recent", CreatedAt = DateTime.UtcNow };
        var older = new ThreadMessageItemResponse { Id = Guid.NewGuid(), Text = "match", CreatedAt = DateTime.UtcNow.AddDays(-1) };
        fixture.Session.Setup(s => s.GetMessagesAsync(fixture.Thread, 0, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ChatFixture.Ok(new GetThreadMessagesResponse { Items = [recent], TotalCount = 2 }));
        fixture.Session.Setup(s => s.GetMessagesAsync(fixture.Thread, 1, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ChatFixture.Ok(new GetThreadMessagesResponse { Items = [older], TotalCount = 2 }));
        await using var workspace = fixture.Workspace();
        await workspace.StartAsync("device");
        await workspace.OpenMessageAsync(fixture.Thread, older.Id);
        Assert.That(workspace.Messages.Select(m => m.Id), Is.EqualTo(new[] { older.Id, recent.Id }));
    }

    [Test]
    public async Task OpenMessageAsync_DeletedResult_ReportsMissingWithoutLooping()
    {
        var fixture = new ChatFixture();
        fixture.Session.Setup(s => s.GetMessagesAsync(fixture.Thread, It.IsAny<int>(), 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ChatFixture.Ok(new GetThreadMessagesResponse { TotalCount = 1 }));
        await using var workspace = fixture.Workspace();
        await workspace.StartAsync("device");
        Assert.ThrowsAsync<ChatOperationException>(async () => await workspace.OpenMessageAsync(fixture.Thread, Guid.NewGuid()));
        fixture.Session.Verify(s => s.GetMessagesAsync(fixture.Thread, It.IsAny<int>(), 50, It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Test]
    public async Task ToggleReactionAsync_OwnReaction_RemovesExactReactionInsteadOfOtherMembers()
    {
        var fixture = new ChatFixture();
        var id = Guid.NewGuid();
        var reactionId = Guid.NewGuid();
        fixture.Session.Setup(s => s.DeleteReactionAsync(fixture.Thread, id, reactionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CmdResponse { HttpStatusCode = HttpStatusCode.OK });
        var message = new Yap.Models.ChatMessage(id, fixture.Credential, "You", "hello", DateTime.UtcNow, true, null, false, false)
        { Reactions = [new() { TypeId = fixture.HeartType, Count = 2, MyReactionId = reactionId }] };
        await using var workspace = fixture.Workspace();
        await workspace.StartAsync("device");
        await workspace.ToggleReactionAsync(fixture.Thread, message, fixture.HeartType);
        fixture.Session.Verify(s => s.DeleteReactionAsync(fixture.Thread, id, reactionId, It.IsAny<CancellationToken>()), Times.Once);
        fixture.Session.Verify(s => s.ReactAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ToggleReactionAsync_OnlyOtherMembersReacted_AddsOwnReaction()
    {
        var fixture = new ChatFixture();
        var id = Guid.NewGuid();
        fixture.Session.Setup(s => s.ReactAsync(fixture.Thread, id, fixture.HeartType, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CmdResponse { HttpStatusCode = HttpStatusCode.OK });
        var message = new Yap.Models.ChatMessage(id, fixture.Credential, "You", "hello", DateTime.UtcNow, true, null, false, false)
        { Reactions = [new() { TypeId = fixture.HeartType, Count = 1 }] };
        await using var workspace = fixture.Workspace();
        await workspace.StartAsync("device");
        await workspace.ToggleReactionAsync(fixture.Thread, message, fixture.HeartType);
        fixture.Session.Verify(s => s.ReactAsync(fixture.Thread, id, fixture.HeartType, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task OpenRepliesAsync_RealtimeRefresh_PreservesParentScopeAndLoadedPage()
    {
        var fixture = new ChatFixture();
        var parent = new Yap.Models.ChatMessage(Guid.NewGuid(), fixture.Credential, "You", "Topic", DateTime.UtcNow, true, null, false, false);
        fixture.Session.Setup(s => s.GetRepliesAsync(fixture.Thread, parent.Id, 0, 30, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ChatFixture.Ok(new GetThreadMessagesResponse { Items = [new() { Id = Guid.NewGuid(), Text = "Reply", ParentMessageId = parent.Id }], TotalCount = 1 }));
        await using var workspace = fixture.Workspace();
        await workspace.StartAsync("device");
        await workspace.SelectAsync(fixture.Thread);
        await workspace.OpenRepliesAsync(parent);
        await fixture.OnEvent!(new() { TenantId = fixture.Tenant, ThreadId = fixture.Thread });
        Assert.That(workspace.Replies.Single().ParentId, Is.EqualTo(parent.Id));
        fixture.Session.Verify(s => s.GetRepliesAsync(fixture.Thread, parent.Id, 0, 30, It.IsAny<CancellationToken>()), Times.Exactly(2));
        await workspace.CloseRepliesAsync();
        Assert.That(workspace.Replies, Is.Empty);
    }
}

internal sealed class ChatFixture
{
    public Guid Tenant { get; } = Guid.NewGuid();
    public Guid Credential { get; } = Guid.NewGuid();
    public Guid Thread { get; } = Guid.NewGuid();
    public Guid HeartType { get; } = Guid.NewGuid();
    public Mock<ICommunicationsChatSession> Session { get; } = new();
    public Mock<ICommunicationsChatClient> Client { get; } = new();
    public Func<CommunicationsRealtimeEvent, Task>? OnEvent { get; private set; }
    public CancellationToken SubscriptionToken { get; private set; }
    public ChatFixture()
    {
        Session.SetupGet(s => s.TenantId).Returns(Tenant);
        Session.SetupGet(s => s.CredentialId).Returns(Credential);
        Session.Setup(s => s.EnsureChatDefaultsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Ok(new ChatReferenceDataResponse { ThreadTypeId = Guid.NewGuid(), MessageTypeId = Guid.NewGuid(),
                ReactionTypes = [new() { Id = HeartType, Name = "Heart", Emoji = "❤️" }] }));
        Session.Setup(s => s.GetRepliesAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Ok(new GetThreadMessagesResponse()));
        Session.Setup(s => s.GetThreadsAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Ok(new GetThreadListResponse()));
        Session.Setup(s => s.GetThreadAsync(Thread, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Ok(new GetThreadResponse { Id = Thread, Name = "Test conversation" }));
        Session.Setup(s => s.GetMessagesAsync(Thread, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Ok(new GetThreadMessagesResponse()));
        Session.Setup(s => s.SubscribeUserEventsAsync(It.IsAny<Func<CommunicationsRealtimeEvent, Task>>(), It.IsAny<CancellationToken>()))
            .Callback<Func<CommunicationsRealtimeEvent, Task>, CancellationToken>((handler, ct) => { OnEvent = handler; SubscriptionToken = ct; })
            .Returns(Task.CompletedTask);
        Session.Setup(s => s.MarkReadAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CmdResponse { HttpStatusCode = HttpStatusCode.OK });
        Client.Setup(c => c.ForCurrentActorAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(Session.Object);
    }
    public ChatWorkspace Workspace()
    {
        var directory = new Mock<IChatDirectory>();
        directory.Setup(d => d.ResolveAsync(It.IsAny<Guid[]>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        return new(Client.Object, directory.Object, NullLogger<ChatWorkspace>.Instance);
    }
    public static QueryResponse<T> Ok<T>(T value) => new() { HttpStatusCode = HttpStatusCode.OK, Response = value };
}
