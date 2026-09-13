using System.Net;
using System.Net.WebSockets;
using System.Security.Claims;
using Bolt.Server;
using Communications.Domain.Shared.Contracts.Requests.Threads;
using Communications.Domain.Shared.Contracts.Responses;
using Communications.Integration.Drivers;
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
    [Test]
    public async Task GroupAdmission_ProductionConstructor_RemainsDisabled()
    {
        await using var f = await Fixture.CreateAsync();
        var error = Assert.ThrowsAsync<YapApiException>(() => f.Gateway.StartGroupAsync(f.Alice, f.Thread, [f.BobId, f.CharlieId]));
        Assert.That(error!.Status, Is.EqualTo(503));
    }

    [Test]
    public async Task GroupConnect_RequiresEachInvitedMemberToAccept()
    {
        await using var f = await Fixture.CreateAsync(groupLifecycle: true);
        var room = await f.Gateway.StartGroupAsync(f.Alice, f.Thread, [f.BobId, f.CharlieId]);
        var denied = Assert.ThrowsAsync<YapApiException>(() => f.Gateway.ConnectGroupAsync(f.Bob, room.Id));
        Assert.That(denied!.Status, Is.EqualTo(403));
        await f.Gateway.AcceptGroupAsync(f.Bob, room.Id);
        Assert.That((await f.Gateway.ConnectGroupAsync(f.Bob, room.Id)).ClientId, Is.EqualTo(YapCallGateway.ClientId(room.Id, f.BobId)));
        Assert.ThrowsAsync<YapApiException>(() => f.Gateway.ConnectGroupAsync(f.Charlie, room.Id));
        Assert.That(f.Gateway.GroupRoster(f.Alice, room.Id).Participants.Count(x => x.Accepted), Is.EqualTo(2));
    }

    [Test]
    public async Task GroupAdmission_NonmemberAndForeignTenant_AreRejected()
    {
        await using var f = await Fixture.CreateAsync(groupLifecycle: true);
        Assert.ThrowsAsync<YapApiException>(() => f.Gateway.StartGroupAsync(f.Alice, f.Thread, [f.BobId, Guid.NewGuid()]));
        var room = await f.Gateway.StartGroupAsync(f.Alice, f.Thread, [f.BobId, f.CharlieId]);
        Assert.That(Assert.Throws<YapApiException>(() => f.Gateway.GroupRoster(f.OtherTenant, room.Id))!.Status, Is.EqualTo(404));
    }

    [Test]
    public async Task GroupAcceptanceAndConnection_RecheckMembership()
    {
        await using var f = await Fixture.CreateAsync(groupLifecycle: true);
        var room = await f.Gateway.StartGroupAsync(f.Alice, f.Thread, [f.BobId, f.CharlieId]);
        await f.Gateway.AcceptGroupAsync(f.Bob, room.Id);
        f.Members.RemoveAll(x => x.CredentialId == f.BobId || x.CredentialId == f.CharlieId);
        Assert.That(Assert.ThrowsAsync<YapApiException>(() => f.Gateway.ConnectGroupAsync(f.Bob, room.Id))!.Status, Is.EqualTo(403));
        Assert.That(Assert.ThrowsAsync<YapApiException>(() => f.Gateway.AcceptGroupAsync(f.Charlie, room.Id))!.Status, Is.EqualTo(403));
    }

    [Test]
    public async Task GroupLeave_RemovesOwnMembershipAndTicket_WithoutEndingRemainingMembers()
    {
        await using var f = await Fixture.CreateAsync(groupLifecycle: true);
        var room = await f.Gateway.StartGroupAsync(f.Alice, f.Thread, [f.BobId, f.CharlieId]);
        await f.Gateway.AcceptGroupAsync(f.Bob, room.Id);
        await f.Gateway.AcceptGroupAsync(f.Charlie, room.Id);
        var ticket = await f.Gateway.ConnectGroupAsync(f.Bob, room.Id);
        await f.Gateway.LeaveGroupAsync(f.Bob, room.Id);
        Assert.That(f.Gateway.GroupRoster(f.Alice, room.Id).Participants.Single(x => x.CredentialId == f.BobId).Left, Is.True);
        Assert.That(f.Gateway.GroupRoster(f.Charlie, room.Id).Participants.Count(x => x.Accepted && !x.Left), Is.EqualTo(2));
        Assert.Throws<YapApiException>(() => f.Gateway.GroupRoster(f.Bob, room.Id));
        Assert.That(Assert.ThrowsAsync<YapApiException>(() => f.Gateway.AcceptSocketAsync(Context(f.Bob, ticket.Url)))!.Status, Is.EqualTo(403));
    }

    [Test]
    public async Task GroupAccept_IsBoundToTheAcceptingSession()
    {
        await using var f = await Fixture.CreateAsync(groupLifecycle: true);
        var room = await f.Gateway.StartGroupAsync(f.Alice, f.Thread, [f.BobId, f.CharlieId]);
        await f.Gateway.AcceptGroupAsync(f.Bob, room.Id);
        var otherIdentity = new ClaimsIdentity(f.Bob.Identity as ClaimsIdentity);
        otherIdentity.RemoveClaim(otherIdentity.FindFirst(YapAuth.SessionClaim)!);
        otherIdentity.AddClaim(new(YapAuth.SessionClaim, "another-session"));
        Assert.That(Assert.ThrowsAsync<YapApiException>(() => f.Gateway.ConnectGroupAsync(new(otherIdentity), room.Id))!.Status, Is.EqualTo(403));
    }

    [Test]
    public async Task GroupReady_BeforeRegistration_IsDenied()
    {
        await using var f = await Fixture.CreateAsync(groupLifecycle: true);
        var room = await f.Gateway.StartGroupAsync(f.Alice, f.Thread, [f.BobId, f.CharlieId]);
        await f.Gateway.AcceptGroupAsync(f.Bob, room.Id);
        Assert.That(Assert.ThrowsAsync<YapApiException>(() => f.Gateway.ReadyGroupAsync(f.Bob, room.Id))!.Status, Is.EqualTo(409));
    }

    [Test]
    public async Task GroupFailedUpgrade_LeavesOnlyFailedMember()
    {
        await using var f = await Fixture.CreateAsync(groupLifecycle: true);
        var room = await f.Gateway.StartGroupAsync(f.Alice, f.Thread, [f.BobId, f.CharlieId]);
        await f.Gateway.AcceptGroupAsync(f.Bob, room.Id);
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

    private sealed class Fixture : IAsyncDisposable
    {
        public Guid Thread { get; } = Guid.NewGuid();
        public Guid BobId { get; private set; }
        public Guid CharlieId { get; private set; }
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
            { ["Yap:Calls:Enabled"] = "true", ["Yap:Calls:SecurityMode"] = "TrustedServerTls" }).Build();
            var wrapper = new Mock<ICommunicationsServiceWrapper>();
            wrapper.Setup(x => x.GetThreadAsync(It.IsAny<GetThreadRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new QueryResponse<GetThreadResponse> { HttpStatusCode = HttpStatusCode.OK,
                    Response = new GetThreadResponse { Id = fixture.Thread, Members = fixture.Members } });
            var actorScope = new Mock<IActorAccessTokenScope>();
            var services = new ServiceCollection().AddLogging().AddDistributedMemoryCache().AddDataProtection().Services;
            services.AddSingleton<IConfiguration>(configuration).AddSingleton(wrapper.Object).AddSingleton(actorScope.Object).AddSingleton<YapSessions>();
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
            fixture.Members.Add(new() { CredentialId = alice.Credential.Id });
            fixture.Members.Add(new() { CredentialId = bob.Credential.Id });
            fixture.Members.Add(new() { CredentialId = charlie.Credential.Id });
            fixture.Gateway = groupLifecycle
                ? new(configuration, fixture.provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<BoltServer>.Instance, enableGroupLifecycle: true)
                : new(configuration, fixture.provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<BoltServer>.Instance);
            return fixture;
        }
        public async ValueTask DisposeAsync() { Gateway.Dispose(); await provider.DisposeAsync(); }
    }
}
