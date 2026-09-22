using System.Net;
using System.Net.WebSockets;
using System.Security.Claims;
using Bolt.Server;
using Communications.Domain.Shared.Contracts.Requests.Threads;
using Communications.Domain.Shared.Contracts.Responses;
using Communications.Integration.Drivers;
using System.Collections.Concurrent;
using Notifications.Domain.Shared.Contracts.Requests;
using Notifications.Domain.Shared.Contracts.Responses;
using Notifications.Integration.Drivers;
using IdentityServer.Integration.Drivers;
using IdentityServer.Domain.Shared.Contracts.Requests;
using IdentityServer.Domain.Shared.Contracts.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using Yap.Contracts;
using Yap.Services;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Integration.Security;

namespace Yap.Tests;

[TestFixture]
public sealed class YapCallGatewayTests
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task VideoIntent_IsAvailableToRecipientBeforeAnyCameraIsActive(bool video)
    {
        await using var f = await Fixture.CreateAsync(groupLifecycle: true);
        var room = await f.Gateway.StartGroupAsync(f.Alice, f.Thread, [f.BobId], deviceId: f.AliceDevice, videoRequested: video);
        Assert.That(room.VideoRequested, Is.EqualTo(video));
        Assert.That(f.Gateway.GroupRoster(f.Bob, room.Id).VideoRequested, Is.EqualTo(video));
        Assert.That(room.Participants.All(x => !x.Video), Is.True);
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    public async Task History_RemembersVideoIntentAndCameraUpgrade(bool requested, bool upgraded)
    {
        await using var f = await Fixture.CreateAsync(groupLifecycle: true);
        var room = await f.Gateway.StartGroupAsync(f.Alice, f.Thread, [f.BobId], deviceId: f.AliceDevice, videoRequested: requested);
        if (upgraded) { f.Gateway.VideoGroup(f.Alice, room.Id, true); f.Gateway.VideoGroup(f.Alice, room.Id, false); }
        await f.Gateway.LeaveGroupAsync(f.Alice, room.Id);
        Assert.That(await f.HistoryArrived.WaitAsync(TimeSpan.FromSeconds(5)), Is.True);
        Assert.That(f.History.Single().Video, Is.EqualTo(requested || upgraded));
    }

    [Test]
    public async Task HistoryFailure_DoesNotBlockHangupAndRetriesTheSameOutcome()
    {
        await using var f = await Fixture.CreateAsync(groupLifecycle: true);
        f.FailHistory = true;
        var room = await f.Gateway.StartGroupAsync(f.Alice, f.Thread, [f.BobId], deviceId: f.AliceDevice);
        await f.Gateway.LeaveGroupAsync(f.Alice, room.Id);
        Assert.That(await f.HistoryFailed.WaitAsync(TimeSpan.FromSeconds(5)), Is.True);
        Assert.Throws<YapApiException>(() => f.Gateway.GroupRoster(f.Bob, room.Id));
        f.FailHistory = false;
        // The same retry used by the cleanup timer; a write still completing may defer one tick.
        for (var n = 0; n < 10 && f.History.IsEmpty; n++)
        { await f.Gateway.FlushCallHistoryAsync(); await Task.Delay(20); }
        Assert.That(f.History.Single().CallId, Is.EqualTo(room.Id));
        await f.Gateway.FlushCallHistoryAsync();
        Assert.That(f.History, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task UnansweredCall_WritesOnePersistentRecordAfterCallerLeaves()
    {
        await using var f = await Fixture.CreateAsync(groupLifecycle: true);
        var room = await f.Gateway.StartGroupAsync(f.Alice, f.Thread, [f.BobId], deviceId: f.AliceDevice);
        await f.Gateway.LeaveGroupAsync(f.Alice, room.Id);
        Assert.That(await f.HistoryArrived.WaitAsync(TimeSpan.FromSeconds(5)), Is.True);
        await f.Gateway.FlushCallHistoryAsync();
        var item = f.History.Single();
        Assert.That(item.CallId, Is.EqualTo(room.Id));
        Assert.That(item.ThreadId, Is.EqualTo(f.Thread));
        Assert.That(item.ConnectedAt, Is.Null);
        Assert.That(item.EndedAt, Is.LessThanOrEqualTo(DateTimeOffset.UtcNow));
    }

    [Test]
    public async Task CallerLeavesBeforeAcceptance_EndsPendingInvitationsAndReleasesParticipants()
    {
        await using var f = await Fixture.CreateAsync(groupLifecycle: true);
        var events = new List<YapCallEvent>();
        using var subscription = f.Gateway.Subscribe(f.Bob, events.Add);
        var room = await f.Gateway.StartGroupAsync(f.Alice, f.Thread, [f.BobId, f.CharlieId], deviceId: f.AliceDevice);
        await f.Gateway.LeaveGroupAsync(f.Alice, room.Id);
        Assert.That(events.Last().Type, Is.EqualTo("group-ended"));
        Assert.That(events.Last().Group!.Participants.All(x => x.Left), Is.True);
        Assert.That(Assert.ThrowsAsync<YapApiException>(() => f.Gateway.AcceptGroupAsync(f.Bob, room.Id, deviceId: f.BobDevice))!.Status, Is.EqualTo(404));
        Assert.That((await f.Gateway.StartGroupAsync(f.Alice, f.Thread, [f.BobId], deviceId: f.AliceDevice)).Id, Is.Not.EqualTo(room.Id));
    }

    [Test]
    public async Task UnacceptedInviteeDeclines_DoesNotRotateTheAcceptedRoster()
    {
        await using var f = await Fixture.CreateAsync(groupLifecycle: true);
        var room = await f.Gateway.StartGroupAsync(f.Alice, f.Thread, [f.BobId, f.CharlieId], deviceId: f.AliceDevice);
        room = await f.Gateway.AcceptGroupAsync(f.Bob, room.Id, deviceId: f.BobDevice);
        await f.Gateway.LeaveGroupAsync(f.Charlie, room.Id);
        var current = f.Gateway.GroupRoster(f.Alice, room.Id);
        Assert.That(current.Revision, Is.EqualTo(room.Revision));
        Assert.That(current.Participants.Single(x => x.CredentialId == f.CharlieId).Left, Is.True);
    }

    [Test]
    public async Task RevokedDevices_CannotStartAcceptConnectOrReceiveNewCallKeys()
    {
        await using var f = await Fixture.CreateAsync(groupLifecycle: true);
        f.RevokedDevices.Add(f.AliceDevice);
        Assert.That(Assert.ThrowsAsync<YapApiException>(() => f.Gateway.StartGroupAsync(f.Alice, f.Thread,
            [f.BobId, f.CharlieId], deviceId: f.AliceDevice))!.Status, Is.EqualTo(403));
        f.RevokedDevices.Clear();
        var room = await f.Gateway.StartGroupAsync(f.Alice, f.Thread, [f.BobId, f.CharlieId], deviceId: f.AliceDevice);
        f.RevokedDevices.Add(f.BobDevice);
        Assert.That(Assert.ThrowsAsync<YapApiException>(() => f.Gateway.AcceptGroupAsync(f.Bob, room.Id, deviceId: f.BobDevice))!.Status, Is.EqualTo(403));
        f.RevokedDevices.Clear();
        room = await f.Gateway.AcceptGroupAsync(f.Bob, room.Id, deviceId: f.BobDevice);
        f.RevokedDevices.Add(f.BobDevice);
        Assert.That(Assert.ThrowsAsync<YapApiException>(() => f.Gateway.ConnectGroupAsync(f.Bob, room.Id))!.Status, Is.EqualTo(403));
        Assert.That(Assert.ThrowsAsync<YapApiException>(() => f.Gateway.RelayGroupControlAsync(f.Alice, room.Id,
            new(room.Revision, 1, f.BobId, "opaque-key")))!.Status, Is.EqualTo(403));
    }

    [Test]
    public async Task ReadyFromAnEarlierMembershipRevision_IsRejected()
    {
        await using var f = await Fixture.CreateAsync(groupLifecycle: true);
        var room = await f.Gateway.StartGroupAsync(f.Alice, f.Thread, [f.BobId, f.CharlieId], deviceId: f.AliceDevice);
        await f.Gateway.AcceptGroupAsync(f.Bob, room.Id, deviceId: f.BobDevice);
        var error = Assert.ThrowsAsync<YapApiException>(() => f.Gateway.ReadyGroupAsync(f.Alice, room.Id, revision: room.Revision));
        Assert.That(error!.Message, Is.EqualTo("The call membership changed."));
    }
    [Test]
    public async Task GroupControls_StampSenderAndReachOnlyAcceptedAddressedRecipient()
    {
        await using var f = await Fixture.CreateAsync(groupLifecycle: true);
        var device = f.AliceDevice;
        var room = await f.Gateway.StartGroupAsync(f.Alice, f.Thread, [f.BobId, f.CharlieId], deviceId: device);
        room = await f.Gateway.AcceptGroupAsync(f.Bob, room.Id, deviceId: f.BobDevice);
        var bob = new List<YapCallEvent>(); var charlie = new List<YapCallEvent>();
        using var one = f.Gateway.Subscribe(f.Bob, bob.Add); using var two = f.Gateway.Subscribe(f.Charlie, charlie.Add);
        await f.Gateway.RelayGroupControlAsync(f.Alice, room.Id, new(room.Revision, 1, f.BobId, "opaque-key"));
        var control = bob.Single(x => x.Type == "group-control").Control!;
        Assert.That(control.SenderDeviceId, Is.EqualTo(device));
        Assert.That(control.SenderId, Is.EqualTo(f.Alice.FindFirstValue(ClaimTypes.NameIdentifier) is { } id ? Guid.Parse(id) : Guid.Empty));
        Assert.That(charlie.Any(x => x.Type == "group-control"), Is.False);
        Assert.That(Assert.ThrowsAsync<YapApiException>(() => f.Gateway.RelayGroupControlAsync(f.Alice, room.Id,
            new(room.Revision, 2, f.CharlieId, "opaque-key")))!.Status, Is.EqualTo(403));
    }

    [Test]
    public async Task GroupControls_ReplayKeyAndAck_RejectDuplicatesAndStaleEpochs()
    {
        await using var f = await Fixture.CreateAsync(groupLifecycle: true);
        var room = await f.Gateway.StartGroupAsync(f.Alice, f.Thread, [f.BobId, f.CharlieId], deviceId: f.AliceDevice);
        room = await f.Gateway.AcceptGroupAsync(f.Bob, room.Id, deviceId: f.BobDevice);
        await f.Gateway.RelayGroupControlAsync(f.Alice, room.Id, new(room.Revision, 1, f.BobId, "key"));
        await f.Gateway.RelayGroupControlAsync(f.Alice, room.Id, new(room.Revision, 2, f.BobId, "ack", "ack"));
        Assert.That(Assert.ThrowsAsync<YapApiException>(() => f.Gateway.RelayGroupControlAsync(f.Alice, room.Id,
            new(room.Revision, 1, f.BobId, "replay")))!.Status, Is.EqualTo(409));
        var received = new List<YapCallEvent>();
        using (f.Gateway.Subscribe(f.Bob, received.Add)) Assert.That(received.Count(x => x.Type == "group-control"), Is.EqualTo(2));
        await f.Gateway.AcceptGroupAsync(f.Charlie, room.Id, deviceId: f.CharlieDevice);
        Assert.That(Assert.ThrowsAsync<YapApiException>(() => f.Gateway.RelayGroupControlAsync(f.Alice, room.Id,
            new(room.Revision, 3, f.BobId, "stale")))!.Status, Is.EqualTo(409));
        received.Clear();
        using (f.Gateway.Subscribe(f.Bob, received.Add)) Assert.That(received.Any(x => x.Type == "group-control"), Is.False);
    }

    [Test]
    public async Task GroupControls_RejectWrongSessionRevokedMembershipAndOversizedPayload()
    {
        await using var f = await Fixture.CreateAsync(groupLifecycle: true);
        var room = await f.Gateway.StartGroupAsync(f.Alice, f.Thread, [f.BobId, f.CharlieId], deviceId: f.AliceDevice);
        room = await f.Gateway.AcceptGroupAsync(f.Bob, room.Id, deviceId: f.BobDevice);
        Assert.That(Assert.ThrowsAsync<YapApiException>(() => f.Gateway.RelayGroupControlAsync(f.Alice, room.Id,
            new(room.Revision, 1, f.BobId, new string('a', 32769))))!.Status, Is.EqualTo(400));
        var wrongSession = new ClaimsPrincipal(new ClaimsIdentity(f.Alice.Claims.Where(x => x.Type != YapAuth.SessionClaim)
            .Append(new Claim(YapAuth.SessionClaim, Guid.NewGuid().ToString())), "test"));
        Assert.That(Assert.ThrowsAsync<YapApiException>(() => f.Gateway.RelayGroupControlAsync(wrongSession, room.Id,
            new(room.Revision, 1, f.BobId, "key")))!.Status, Is.EqualTo(403));
        f.Members.RemoveAll(x => x.CredentialId == f.BobId);
        Assert.That(Assert.ThrowsAsync<YapApiException>(() => f.Gateway.RelayGroupControlAsync(f.Alice, room.Id,
            new(room.Revision, 1, f.BobId, "key")))!.Status, Is.EqualTo(403));
    }

    [Test]
    public async Task GroupMuteChangesOwnPresenceWithoutChangingKeyEpoch()
    {
        await using var f = await Fixture.CreateAsync(groupLifecycle: true);
        var room = await f.Gateway.StartGroupAsync(f.Alice, f.Thread, [f.BobId, f.CharlieId], deviceId: f.AliceDevice);
        room = await f.Gateway.AcceptGroupAsync(f.Bob, room.Id, deviceId: f.BobDevice);
        f.Gateway.MuteGroup(f.Bob, room.Id, true);
        var next = f.Gateway.GroupRoster(f.Bob, room.Id);
        Assert.That(next.Revision, Is.EqualTo(room.Revision));
        Assert.That(next.Participants.Single(x => x.CredentialId == f.BobId).Muted, Is.True);
        Assert.That(next.Participants.Count(x => x.Muted), Is.EqualTo(1));
    }

    // Camera state is roster presence, exactly like mute: it must not rotate the epoch, because a
    // rekey pauses everybody's media and a camera button should never interrupt the audio.
    [Test]
    public async Task GroupVideoFlag_TracksTheCameraWithoutChangingTheKeyEpoch()
    {
        await using var f = await Fixture.CreateAsync(groupLifecycle: true);
        var room = await f.Gateway.StartGroupAsync(f.Alice, f.Thread, [f.BobId, f.CharlieId], deviceId: f.AliceDevice);
        room = await f.Gateway.AcceptGroupAsync(f.Bob, room.Id, deviceId: f.BobDevice);
        Assert.That(room.Participants, Has.None.Matches<YapGroupParticipant>(x => x.Video), "no camera is on until someone turns one on");

        f.Gateway.VideoGroup(f.Bob, room.Id, true);
        var next = f.Gateway.GroupRoster(f.Alice, room.Id);
        Assert.Multiple(() =>
        {
            Assert.That(next.Revision, Is.EqualTo(room.Revision));
            Assert.That(next.Participants.Single(x => x.CredentialId == f.BobId).Video, Is.True);
            Assert.That(next.Participants.Count(x => x.Video), Is.EqualTo(1));
        });

        f.Gateway.VideoGroup(f.Bob, room.Id, false);
        Assert.That(f.Gateway.GroupRoster(f.Alice, room.Id).Participants, Has.None.Matches<YapGroupParticipant>(x => x.Video));
    }

    [Test]
    public async Task AParticipantWhoLeaves_StopsBeingListedAsOnCamera()
    {
        await using var f = await Fixture.CreateAsync(groupLifecycle: true);
        var room = await f.Gateway.StartGroupAsync(f.Alice, f.Thread, [f.BobId, f.CharlieId], deviceId: f.AliceDevice);
        await f.Gateway.AcceptGroupAsync(f.Bob, room.Id, deviceId: f.BobDevice);
        f.Gateway.VideoGroup(f.Bob, room.Id, true);
        await f.Gateway.LeaveGroupAsync(f.Bob, room.Id);
        Assert.That(f.Gateway.GroupRoster(f.Alice, room.Id).Participants, Has.None.Matches<YapGroupParticipant>(x => x.Video));
    }

    [TestCase(true, true)]
    [TestCase(false, false)]
    public void Video_FollowsTheEncryptedGroupGateAndItsOwnSwitch(bool video, bool expected)
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["Yap:Calls:Enabled"] = "true", ["Yap:Calls:EncryptedGroups"] = "true",
          ["Yap:Calls:SecurityMode"] = "EndToEndEncrypted", ["Yap:Calls:Video"] = video.ToString() }).Build();
        using var gateway = new YapCallGateway(config, services.GetRequiredService<IServiceScopeFactory>(), NullLogger<BoltServer>.Instance);
        Assert.That(gateway.VideoEnabled, Is.EqualTo(expected));
    }

    // Without encrypted groups there is no epoch key, so there is nothing to encrypt a picture with.
    [Test]
    public void Video_IsNeverAvailableWithoutEndToEndEncryptedGroups()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["Yap:Calls:Enabled"] = "true", ["Yap:Calls:EncryptedGroups"] = "false",
          ["Yap:Calls:SecurityMode"] = "TrustedServerTls", ["Yap:Calls:Video"] = "true" }).Build();
        using var gateway = new YapCallGateway(config, services.GetRequiredService<IServiceScopeFactory>(), NullLogger<BoltServer>.Instance);
        Assert.That(gateway.VideoEnabled, Is.False);
    }

    [Test]
    public async Task GroupAdmission_ProductionConstructor_DefaultsToDisabled()
    {
        await using var f = await Fixture.CreateAsync();
        var error = Assert.ThrowsAsync<YapApiException>(() => f.Gateway.StartGroupAsync(f.Alice, f.Thread, [f.BobId, f.CharlieId], deviceId: f.AliceDevice));
        Assert.That(error!.Status, Is.EqualTo(503));
    }

    [TestCase(false, true, "EndToEndEncrypted", false)]
    [TestCase(true, false, "TrustedServerTls", false)]
    [TestCase(true, true, "EndToEndEncrypted", true)]
    public void EncryptedGroups_RequireBothExplicitGateAndCallsEnabled(bool enabled, bool groups, string mode, bool expected)
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["Yap:Calls:Enabled"] = enabled.ToString(), ["Yap:Calls:EncryptedGroups"] = groups.ToString(), ["Yap:Calls:SecurityMode"] = mode }).Build();
        using var gateway = new YapCallGateway(config, services.GetRequiredService<IServiceScopeFactory>(), NullLogger<BoltServer>.Instance);
        Assert.That(gateway.EncryptedGroupsEnabled, Is.EqualTo(expected));
    }

    [TestCase(null)]
    [TestCase("TrustedServerTls")]
    public void EncryptedGroups_RejectMissingOrTransportOnlySecurityMode(string? mode)
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["Yap:Calls:Enabled"] = "true", ["Yap:Calls:EncryptedGroups"] = "true", ["Yap:Calls:SecurityMode"] = mode }).Build();
        Assert.Throws<InvalidOperationException>(() => new YapCallGateway(config, services.GetRequiredService<IServiceScopeFactory>(), NullLogger<BoltServer>.Instance));
    }

    [Test]
    public async Task GroupConnect_RequiresEachInvitedMemberToAccept()
    {
        await using var f = await Fixture.CreateAsync(groupLifecycle: true);
        var room = await f.Gateway.StartGroupAsync(f.Alice, f.Thread, [f.BobId, f.CharlieId], deviceId: f.AliceDevice);
        var denied = Assert.ThrowsAsync<YapApiException>(() => f.Gateway.ConnectGroupAsync(f.Bob, room.Id));
        Assert.That(denied!.Status, Is.EqualTo(403));
        await f.Gateway.AcceptGroupAsync(f.Bob, room.Id, deviceId: f.BobDevice);
        Assert.That((await f.Gateway.ConnectGroupAsync(f.Bob, room.Id)).ClientId, Is.EqualTo(YapCallGateway.ClientId(room.Id, f.BobId)));
        Assert.ThrowsAsync<YapApiException>(() => f.Gateway.ConnectGroupAsync(f.Charlie, room.Id));
        Assert.That(f.Gateway.GroupRoster(f.Alice, room.Id).Participants.Count(x => x.Accepted), Is.EqualTo(2));
    }

    [Test]
    public async Task GroupAdmission_NonmemberAndForeignTenant_AreRejected()
    {
        await using var f = await Fixture.CreateAsync(groupLifecycle: true);
        Assert.ThrowsAsync<YapApiException>(() => f.Gateway.StartGroupAsync(f.Alice, f.Thread, [f.BobId, Guid.NewGuid()]));
        var room = await f.Gateway.StartGroupAsync(f.Alice, f.Thread, [f.BobId, f.CharlieId], deviceId: f.AliceDevice);
        Assert.That(Assert.Throws<YapApiException>(() => f.Gateway.GroupRoster(f.OtherTenant, room.Id))!.Status, Is.EqualTo(404));
    }

    [Test]
    public async Task GroupAcceptanceAndConnection_RecheckMembership()
    {
        await using var f = await Fixture.CreateAsync(groupLifecycle: true);
        var room = await f.Gateway.StartGroupAsync(f.Alice, f.Thread, [f.BobId, f.CharlieId], deviceId: f.AliceDevice);
        await f.Gateway.AcceptGroupAsync(f.Bob, room.Id, deviceId: f.BobDevice);
        f.Members.RemoveAll(x => x.CredentialId == f.BobId || x.CredentialId == f.CharlieId);
        Assert.That(Assert.ThrowsAsync<YapApiException>(() => f.Gateway.ConnectGroupAsync(f.Bob, room.Id))!.Status, Is.EqualTo(403));
        Assert.That(Assert.ThrowsAsync<YapApiException>(() => f.Gateway.AcceptGroupAsync(f.Charlie, room.Id, deviceId: f.CharlieDevice))!.Status, Is.EqualTo(403));
    }

    [Test]
    public async Task GroupLeave_RemovesOwnMembershipAndTicket_WithoutEndingRemainingMembers()
    {
        await using var f = await Fixture.CreateAsync(groupLifecycle: true);
        var room = await f.Gateway.StartGroupAsync(f.Alice, f.Thread, [f.BobId, f.CharlieId], deviceId: f.AliceDevice);
        await f.Gateway.AcceptGroupAsync(f.Bob, room.Id, deviceId: f.BobDevice);
        await f.Gateway.AcceptGroupAsync(f.Charlie, room.Id, deviceId: f.CharlieDevice);
        var ticket = await f.Gateway.ConnectGroupAsync(f.Bob, room.Id);
        await f.Gateway.LeaveGroupAsync(f.Bob, room.Id);
        Assert.That(f.Gateway.GroupRoster(f.Alice, room.Id).Participants.Single(x => x.CredentialId == f.BobId).Left, Is.True);
        Assert.That(f.Gateway.GroupRoster(f.Charlie, room.Id).Participants.Count(x => x.Accepted && !x.Left), Is.EqualTo(2));
        Assert.Throws<YapApiException>(() => f.Gateway.GroupRoster(f.Bob, room.Id));
        Assert.That(Assert.ThrowsAsync<YapApiException>(() => f.Gateway.AcceptSocketAsync(Context(f.Bob, ticket.Url)))!.Status, Is.EqualTo(403));
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task TwoPersonCall_HangupEndsRoomForBothParticipants(bool callerLeaves)
    {
        await using var f = await Fixture.CreateAsync(groupLifecycle: true);
        var room = await f.Gateway.StartGroupAsync(f.Alice, f.Thread, [f.BobId], deviceId: f.AliceDevice);
        await f.Gateway.AcceptGroupAsync(f.Bob, room.Id, deviceId: f.BobDevice);
        await f.Gateway.LeaveGroupAsync(callerLeaves ? f.Alice : f.Bob, room.Id);
        Assert.Throws<YapApiException>(() => f.Gateway.GroupRoster(f.Alice, room.Id));
        Assert.Throws<YapApiException>(() => f.Gateway.GroupRoster(f.Bob, room.Id));
        // Neither person remains busy in an already-ended call.
        await f.Gateway.StartGroupAsync(f.Alice, f.Thread, [f.BobId], deviceId: f.AliceDevice);
    }

    [Test]
    public async Task DecliningOnlyInvite_EndsRoomInsteadOfRingingForever()
    {
        await using var f = await Fixture.CreateAsync(groupLifecycle: true);
        var room = await f.Gateway.StartGroupAsync(f.Alice, f.Thread, [f.BobId], deviceId: f.AliceDevice);
        await f.Gateway.LeaveGroupAsync(f.Bob, room.Id);
        Assert.Throws<YapApiException>(() => f.Gateway.GroupRoster(f.Alice, room.Id));
    }

    [Test]
    public async Task GroupAccept_IsBoundToTheAcceptingSession()
    {
        await using var f = await Fixture.CreateAsync(groupLifecycle: true);
        var room = await f.Gateway.StartGroupAsync(f.Alice, f.Thread, [f.BobId, f.CharlieId], deviceId: f.AliceDevice);
        await f.Gateway.AcceptGroupAsync(f.Bob, room.Id, deviceId: f.BobDevice);
        var otherIdentity = new ClaimsIdentity(f.Bob.Identity as ClaimsIdentity);
        otherIdentity.RemoveClaim(otherIdentity.FindFirst(YapAuth.SessionClaim)!);
        otherIdentity.AddClaim(new(YapAuth.SessionClaim, "another-session"));
        Assert.That(Assert.ThrowsAsync<YapApiException>(() => f.Gateway.ConnectGroupAsync(new(otherIdentity), room.Id))!.Status, Is.EqualTo(403));
    }

    [Test]
    public async Task GroupReady_BeforeRegistration_IsDenied()
    {
        await using var f = await Fixture.CreateAsync(groupLifecycle: true);
        var room = await f.Gateway.StartGroupAsync(f.Alice, f.Thread, [f.BobId, f.CharlieId], deviceId: f.AliceDevice);
        await f.Gateway.AcceptGroupAsync(f.Bob, room.Id, deviceId: f.BobDevice);
        Assert.That(Assert.ThrowsAsync<YapApiException>(() => f.Gateway.ReadyGroupAsync(f.Bob, room.Id))!.Status, Is.EqualTo(409));
    }

    [Test]
    public async Task GroupFailedUpgrade_LeavesOnlyFailedMember()
    {
        await using var f = await Fixture.CreateAsync(groupLifecycle: true);
        var room = await f.Gateway.StartGroupAsync(f.Alice, f.Thread, [f.BobId, f.CharlieId], deviceId: f.AliceDevice);
        await f.Gateway.AcceptGroupAsync(f.Bob, room.Id, deviceId: f.BobDevice);
        var ticket = await f.Gateway.ConnectGroupAsync(f.Bob, room.Id);
        Assert.ThrowsAsync<IOException>(() => f.Gateway.AcceptSocketAsync(Context(f.Bob, ticket.Url)));
        var roster = f.Gateway.GroupRoster(f.Alice, room.Id);
        Assert.That(roster.Participants.Single(x => x.CredentialId == f.BobId).Left, Is.True);
        Assert.That(roster.Participants.Count(x => !x.Left), Is.EqualTo(2));
    }

    [TestCase("https://yap.example", true)]
    [TestCase("https://yap.example:8443", false)]
    [TestCase("https://other.example", false)]
    [TestCase("http://yap.example", false)]
    [TestCase("null", false)]
    [TestCase("", false)]
    public void Origin_MustMatchHttpsAuthority(string origin, bool expected)
    {
        var request = new DefaultHttpContext().Request;
        request.Host = new HostString("yap.example");
        request.Headers.Origin = origin;
        Assert.That(YapCallGateway.HasSameOrigin(request), Is.EqualTo(expected));
    }

    [Test]
    public void Enablement_RequiresExplicitTrustedServerSecurityMode()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Yap:Calls:Enabled"] = "true" }).Build();
        Assert.Throws<InvalidOperationException>(() => new YapCallGateway(config, services.GetRequiredService<IServiceScopeFactory>(), NullLogger<BoltServer>.Instance));
    }

    [Test]
    public async Task Start_OnlyCurrentMembersReceiveInvitation_AndOtherTenantCannotConnect()
    {
        await using var fixture = await Fixture.CreateAsync();
        var received = new List<YapCallEvent>();
        using var subscription = fixture.Gateway.Subscribe(fixture.Bob, received.Add);
        var strangerEvents = new List<YapCallEvent>();
        using var stranger = fixture.Gateway.Subscribe(fixture.OtherTenant, strangerEvents.Add);
        var invite = await fixture.Gateway.StartAsync(fixture.Alice, new(fixture.Thread, fixture.BobId), default);
        Assert.That(received.Single().Invite.Id, Is.EqualTo(invite.Id));
        Assert.That(strangerEvents, Is.Empty);
        var denied = Assert.ThrowsAsync<YapApiException>(() => fixture.Gateway.ConnectAsync(fixture.OtherTenant, invite.Id, default));
        Assert.That(denied!.Status, Is.EqualTo(404));
    }

    [Test]
    public async Task Start_NonmemberRecipient_IsDeniedBeforeInvitation()
    {
        await using var fixture = await Fixture.CreateAsync();
        var error = Assert.ThrowsAsync<YapApiException>(() => fixture.Gateway.StartAsync(fixture.Alice, new(fixture.Thread, Guid.NewGuid()), default));
        Assert.That(error!.Status, Is.EqualTo(403));
    }

    [Test]
    public async Task Connect_MembershipRemovedAfterInvitation_IsDenied()
    {
        await using var fixture = await Fixture.CreateAsync();
        var invite = await fixture.Gateway.StartAsync(fixture.Alice, new(fixture.Thread, fixture.BobId), default);
        fixture.Members.RemoveAll(x => x.CredentialId == fixture.BobId);
        var error = Assert.ThrowsAsync<YapApiException>(() => fixture.Gateway.ConnectAsync(fixture.Bob, invite.Id, default));
        Assert.That(error!.Status, Is.EqualTo(403));
    }

    [TestCase("http", "https://yap.example", 426)]
    [TestCase("https", "https://attacker.example", 403)]
    public async Task Socket_InsecureOrCrossOrigin_RejectsBeforeConsumingTicket(string scheme, string origin, int status)
    {
        await using var fixture = await Fixture.CreateAsync();
        var invite = await fixture.Gateway.StartAsync(fixture.Alice, new(fixture.Thread, fixture.BobId), default);
        var connection = await fixture.Gateway.ConnectAsync(fixture.Alice, invite.Id, default);
        var context = Context(fixture.Alice, connection.Url, scheme, origin);
        var error = Assert.ThrowsAsync<YapApiException>(() => fixture.Gateway.AcceptSocketAsync(context));
        Assert.That(error!.Status, Is.EqualTo(status));
    }

    [Test]
    public async Task Socket_TicketCannotBeUsedByAnotherAccountOrReplayed()
    {
        await using var fixture = await Fixture.CreateAsync();
        var invite = await fixture.Gateway.StartAsync(fixture.Alice, new(fixture.Thread, fixture.BobId), default);
        var connection = await fixture.Gateway.ConnectAsync(fixture.Alice, invite.Id, default);
        var wrongUser = Assert.ThrowsAsync<YapApiException>(() => fixture.Gateway.AcceptSocketAsync(Context(fixture.Bob, connection.Url)));
        Assert.That(wrongUser!.Status, Is.EqualTo(403));
        // Accept fails deliberately after ticket consumption, like a failed network upgrade.
        Assert.ThrowsAsync<IOException>(() => fixture.Gateway.AcceptSocketAsync(Context(fixture.Alice, connection.Url)));
        var replay = Assert.ThrowsAsync<YapApiException>(() => fixture.Gateway.AcceptSocketAsync(Context(fixture.Alice, connection.Url)));
        Assert.That(replay!.Status, Is.EqualTo(403));
    }

    [Test]
    public async Task Ready_BeforeAuthenticatedRegistration_IsDenied()
    {
        await using var fixture = await Fixture.CreateAsync();
        var invite = await fixture.Gateway.StartAsync(fixture.Alice, new(fixture.Thread, fixture.BobId), default);
        var error = Assert.Throws<YapApiException>(() => fixture.Gateway.Ready(fixture.Bob, invite.Id));
        Assert.That(error!.Status, Is.EqualTo(409));
    }

    private static DefaultHttpContext Context(ClaimsPrincipal user, string url, string scheme = "https", string origin = "https://yap.example")
    {
        var context = new DefaultHttpContext { User = user };
        context.Request.Scheme = scheme;
        context.Request.Host = new HostString("yap.example");
        context.Request.Headers.Origin = origin;
        context.Request.QueryString = new QueryString(url[url.IndexOf('?')..]);
        context.Features.Set<IHttpWebSocketFeature>(new FailedUpgrade());
        return context;
    }

    private sealed class FailedUpgrade : IHttpWebSocketFeature
    {
        public bool IsWebSocketRequest => true;
        public Task<WebSocket> AcceptAsync(WebSocketAcceptContext context) => throw new IOException("Test connection interrupted during upgrade");
    }

    // A ringing invite lives for 60 seconds and never reaches the Communications outbox, so the
    // only thing that can wake a closed phone in time is this direct push.
    [Test]
    public async Task StartingAGroupCall_ImmediatelyPushesEveryInvitedDevice()
    {
        await using var f = await Fixture.CreateAsync(groupLifecycle: true);
        var room = await f.Gateway.StartGroupAsync(f.Alice, f.Thread, [f.BobId, f.CharlieId], deviceId: f.AliceDevice);

        Assert.That(await f.PushArrived.WaitAsync(TimeSpan.FromSeconds(5)), Is.True);
        Assert.That(await f.PushArrived.WaitAsync(TimeSpan.FromSeconds(5)), Is.True);
        var pushes = f.Pushes.ToArray();
        Assert.That(pushes.Select(x => x.RecipientCredentialId), Is.EquivalentTo(new[] { f.BobId, f.CharlieId }));
        Assert.That(pushes.All(x => x.Kind == "call" && x.Urgency == "high"), Is.True);
        Assert.That(pushes.All(x => x.ThreadId == f.Thread && x.Reference == room.Id.ToString("N")), Is.True);
        // Expiring at the push service beats waking a phone for a call that already timed out.
        Assert.That(pushes.All(x => x.TimeToLiveSeconds <= YapCallGateway.InviteLifetime.TotalSeconds), Is.True);
        Assert.That(pushes.All(x => x.ExpiresAt == room.ExpiresAt), Is.True);
        // The payload must never carry the caller's name or anything else about the conversation.
        Assert.That(System.Text.Json.JsonSerializer.Serialize(pushes[0]), Does.Not.Contain("Someone"));
    }

    [Test]
    public async Task StartingADirectCall_PushesOnlyTheRecipient()
    {
        await using var f = await Fixture.CreateAsync();
        var invite = await f.Gateway.StartAsync(f.Alice, new StartYapCall(f.Thread, f.BobId), default);

        Assert.That(await f.PushArrived.WaitAsync(TimeSpan.FromSeconds(5)), Is.True);
        Assert.That(f.Pushes.Single().RecipientCredentialId, Is.EqualTo(f.BobId));
        Assert.That(f.Pushes.Single().Reference, Is.EqualTo(invite.Id.ToString("N")));
    }

    // A push held by a push service until the end of its TTL still arrives. The absolute deadline
    // travels with it so a worker woken late can tell a live invitation from a dead one.
    [Test]
    public async Task StartingACall_SendsTheInviteDeadlineAndATimeToLiveThatCannotOutliveIt()
    {
        await using var f = await Fixture.CreateAsync();
        var invite = await f.Gateway.StartAsync(f.Alice, new StartYapCall(f.Thread, f.BobId), default);

        Assert.That(await f.PushArrived.WaitAsync(TimeSpan.FromSeconds(5)), Is.True);
        var push = f.Pushes.Single();
        Assert.That(push.Urgency, Is.EqualTo("high"));
        Assert.That(push.ExpiresAt, Is.EqualTo(invite.ExpiresAt));
        Assert.That(push.TimeToLiveSeconds, Is.GreaterThan(0));
        Assert.That(push.TimeToLiveSeconds, Is.LessThanOrEqualTo((invite.ExpiresAt - DateTimeOffset.UtcNow).TotalSeconds + 1));
    }

    // The bug a fixed TTL hides: the invite starts ticking when the gateway creates it, but the
    // push is queued, scoped and sent afterwards. Only a TTL measured against the real deadline
    // shrinks with that delay instead of extending delivery past it.
    [Test]
    public void PushTimeToLive_ShrinksWithProcessingDelayAndStopsAtTheDeadline()
    {
        var start = DateTimeOffset.UtcNow;
        var expires = start.Add(YapCallGateway.InviteLifetime);

        Assert.That(YapCallGateway.RemainingSeconds(expires, start), Is.EqualTo(60));
        Assert.That(YapCallGateway.RemainingSeconds(expires, start.AddSeconds(12)), Is.EqualTo(48));
        // Floored, never rounded up: the TTL may only ever run out before the invite does.
        Assert.That(YapCallGateway.RemainingSeconds(expires, start.AddSeconds(12.9)), Is.EqualTo(47));
        Assert.That(YapCallGateway.RemainingSeconds(expires, expires), Is.EqualTo(0));
        Assert.That(YapCallGateway.RemainingSeconds(expires, expires.AddSeconds(30)), Is.EqualTo(0));
    }

    // Tapping a notification only surfaces the app; everything it does next goes through these
    // authenticated entry points, which is where a dead call has to stay dead.
    [Test]
    public async Task ExpiredCall_IsNeitherReplayedNorConnectableWhenTheAppOpens()
    {
        await using var f = await Fixture.CreateAsync();
        var invite = await f.Gateway.StartAsync(f.Alice, new StartYapCall(f.Thread, f.BobId), default);
        Expire(f.Gateway, invite.Id);

        var events = new List<YapCallEvent>();
        using var subscription = f.Gateway.Subscribe(f.Bob, events.Add);
        Assert.That(events, Is.Empty, "an expired invite must never ring again on a device that reconnects");
        Assert.That(Assert.ThrowsAsync<YapApiException>(() => f.Gateway.ConnectAsync(f.Bob, invite.Id, default))!.Status, Is.EqualTo(404));
    }

    [Test]
    public async Task EndedCall_IsNeitherReplayedNorConnectableWhenTheAppOpens()
    {
        await using var f = await Fixture.CreateAsync();
        var invite = await f.Gateway.StartAsync(f.Alice, new StartYapCall(f.Thread, f.BobId), default);
        f.Gateway.End(f.Alice, invite.Id);

        var events = new List<YapCallEvent>();
        using var subscription = f.Gateway.Subscribe(f.Bob, events.Add);
        Assert.That(events, Is.Empty);
        Assert.That(Assert.ThrowsAsync<YapApiException>(() => f.Gateway.ConnectAsync(f.Bob, invite.Id, default))!.Status, Is.EqualTo(404));
    }

    // Invite expiry is wall-clock. Rather than sleeping out a 60 second invite, the stored one is
    // rewritten with a deadline in the past so the real guards run against a real expired call.
    private static void Expire(YapCallGateway gateway, Guid callId)
    {
        var field = typeof(YapCallGateway).GetField("invites", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var invites = (System.Collections.IDictionary)field.GetValue(gateway)!;
        var active = invites[callId]!;
        var type = active.GetType();
        var invite = (YapCallInvite)type.GetProperty("Invite")!.GetValue(active)!;
        invites[callId] = Activator.CreateInstance(type, type.GetProperty("Tenant")!.GetValue(active),
            invite with { ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1) })!;
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public Guid Thread { get; } = Guid.NewGuid();
        public Guid BobId { get; private set; }
        public Guid CharlieId { get; private set; }
        public Guid AliceDevice { get; } = Guid.NewGuid();
        public Guid BobDevice { get; } = Guid.NewGuid();
        public Guid CharlieDevice { get; } = Guid.NewGuid();
        public HashSet<Guid> RevokedDevices { get; } = [];
        public ConcurrentQueue<SendDirectPushRequest> Pushes { get; } = new();
        public ConcurrentQueue<RecordCallRequest> History { get; } = new();
        public SemaphoreSlim HistoryArrived { get; } = new(0);
        public bool FailHistory;
        public SemaphoreSlim HistoryFailed { get; } = new(0);
        public SemaphoreSlim PushArrived { get; } = new(0);
        public List<ThreadMemberResponse> Members { get; } = [];
        public ClaimsPrincipal Alice { get; private set; } = null!;
        public ClaimsPrincipal Bob { get; private set; } = null!;
        public ClaimsPrincipal Charlie { get; private set; } = null!;
        public ClaimsPrincipal OtherTenant { get; private set; } = null!;
        public YapCallGateway Gateway { get; private set; } = null!;
        private ServiceProvider provider = null!;

        public static async Task<Fixture> CreateAsync(bool groupLifecycle = false)
        {
            var fixture = new Fixture();
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["Yap:Calls:Enabled"] = "true", ["Yap:Calls:EncryptedGroups"] = groupLifecycle.ToString(), ["Yap:Calls:SecurityMode"] = groupLifecycle ? "EndToEndEncrypted" : "TrustedServerTls" }).Build();
            var wrapper = new Mock<ICommunicationsServiceWrapper>();
            wrapper.Setup(x => x.RecordCall(It.IsAny<RecordCallRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((RecordCallRequest request, CancellationToken _) =>
                {
                    if (fixture.FailHistory)
                    { fixture.HistoryFailed.Release(); return new CmdResponse { HttpStatusCode = HttpStatusCode.ServiceUnavailable }; }
                    fixture.History.Enqueue(request); fixture.HistoryArrived.Release();
                    return new CmdResponse { HttpStatusCode = HttpStatusCode.OK };
                });
            wrapper.Setup(x => x.GetThreadAsync(It.IsAny<GetThreadRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new QueryResponse<GetThreadResponse> { HttpStatusCode = HttpStatusCode.OK,
                    Response = new GetThreadResponse { Id = fixture.Thread, Members = fixture.Members } });
            var actorScope = new Mock<IActorAccessTokenScope>();
            var identity = new Mock<IIdentityServerServiceWrapper>();
            var notifications = new Mock<INotificationsServiceWrapper>();
            notifications.Setup(x => x.SendDirectPush(It.IsAny<SendDirectPushRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((SendDirectPushRequest request, CancellationToken _) =>
                {
                    fixture.Pushes.Enqueue(request);
                    fixture.PushArrived.Release();
                    return new QueryResponse<SendDirectPushResponse> { HttpStatusCode = HttpStatusCode.OK, Response = new() { Delivered = 1 } };
                });
            var services = new ServiceCollection().AddLogging().AddDistributedMemoryCache().AddDataProtection().Services;
            services.AddSingleton<IConfiguration>(configuration).AddSingleton(wrapper.Object).AddSingleton(identity.Object).AddSingleton(actorScope.Object).AddSingleton(notifications.Object).AddSingleton(TimeProvider.System).AddSingleton<YapSessions>();
            fixture.provider = services.BuildServiceProvider();
            var sessions = fixture.provider.GetRequiredService<YapSessions>();
            var alice = YapSessionsTests.Session();
            var bob = YapSessionsTests.Session();
            var charlie = YapSessionsTests.Session();
            bob.Credential!.TenantId = alice.Credential!.TenantId;
            charlie.Credential!.TenantId = alice.Credential.TenantId;
            fixture.BobId = bob.Credential.Id;
            fixture.CharlieId = charlie.Credential.Id;
            fixture.Alice = await sessions.CreateAsync(alice);
            fixture.Bob = await sessions.CreateAsync(bob);
            fixture.Charlie = await sessions.CreateAsync(charlie);
            fixture.OtherTenant = await sessions.CreateAsync(YapSessionsTests.Session());
            identity.Setup(x => x.GetEncryptionDirectory(It.IsAny<GetEncryptionDirectoryRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((GetEncryptionDirectoryRequest request, CancellationToken _) =>
                {
                    var device = request.CredentialId == alice.Credential.Id ? fixture.AliceDevice : request.CredentialId == bob.Credential.Id ? fixture.BobDevice : fixture.CharlieDevice;
                    return new QueryResponse<EncryptionDirectoryResponse> { HttpStatusCode = HttpStatusCode.OK, Response = new()
                    { TenantId = alice.Credential.TenantId, CredentialId = request.CredentialId,
                        Devices = [new() { DeviceId = device, Revocation = fixture.RevokedDevices.Contains(device) ? "revoked" : null }] } };
                });
            fixture.Members.Add(new() { CredentialId = alice.Credential.Id });
            fixture.Members.Add(new() { CredentialId = bob.Credential.Id });
            fixture.Members.Add(new() { CredentialId = charlie.Credential.Id });
            fixture.Gateway = new(configuration, fixture.provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<BoltServer>.Instance);
            return fixture;
        }
        public async ValueTask DisposeAsync() { Gateway.Dispose(); await provider.DisposeAsync(); }
    }
}
