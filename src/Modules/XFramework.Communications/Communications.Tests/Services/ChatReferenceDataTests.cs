using Communications.Api.Services;
using Communications.Domain.Shared;
using Communications.Domain.Shared.Configurations;
using Communications.Domain.Shared.Contracts;
using Communications.Domain.Shared.Contracts.Requests.ReferenceData;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Testcontainers.PostgreSql;
using XFramework.Core.Patterns;
using XFramework.Domain.Contexts;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Security;

namespace Communications.Tests.Services;

[TestFixture, Category("Kind:Integration")]
public sealed class ChatReferenceDataTests
{
    private PostgreSqlContainer _postgres = null!;

    [OneTimeSetUp]
    public async Task SetUp()
    {
        _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
        await _postgres.StartAsync();
        await using var connection = new Npgsql.NpgsqlConnection(_postgres.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new Npgsql.NpgsqlCommand("CREATE EXTENSION IF NOT EXISTS \"uuid-ossp\"", connection);
        await command.ExecuteNonQueryAsync();
        await using var db = CreateDb(Guid.NewGuid());
        await db.Database.EnsureCreatedAsync();
    }

    [OneTimeTearDown]
    public async Task TearDown() => await _postgres.DisposeAsync();

    [Test]
    public async Task EnsureAsync_ConcurrentFirstUse_CreatesOneSetAndReusesIds()
    {
        var tenant = Guid.NewGuid();
        var results = await Task.WhenAll(Enumerable.Range(0, 3).Select(async _ =>
        {
            await using var db = CreateDb(tenant);
            return await Service(db, tenant).EnsureAsync(new EnsureChatDefaultsRequest());
        }));
        Assert.That(results.All(x => x.IsSuccess), Is.True, string.Join("; ", results.Select(x => x.Message)));
        Assert.That(results.Select(x => x.Data!.ThreadTypeId).Distinct().Count(), Is.EqualTo(1));
        await using var check = CreateDb(tenant);
        Assert.Multiple(() =>
        {
            Assert.That(check.Set<MessageType>().Count(), Is.EqualTo(1));
            Assert.That(check.Set<MessageThreadType>().Count(), Is.EqualTo(1));
            Assert.That(check.Set<MessageReactionType>().Count(), Is.EqualTo(6));
            Assert.That(check.Set<MessageDeliveryType>().Count(), Is.EqualTo(2));
            Assert.That(check.Set<MessageType>().Single().Id, Is.Not.EqualTo(MessageTypes.Chat));
            Assert.That(check.Set<MessageDeliveryType>().Any(x => x.Id == MessageDeliveryTypes.Read), Is.False);
        });
    }

    [Test]
    public async Task EnsureAsync_TwoTenants_UsesSeparatePrimaryKeysAndDiscovery()
    {
        await using var first = CreateDb(Guid.NewGuid());
        await using var second = CreateDb(Guid.NewGuid());
        var a = await Service(first, first.TenantId).EnsureAsync(new EnsureChatDefaultsRequest());
        var b = await Service(second, second.TenantId).EnsureAsync(new EnsureChatDefaultsRequest());
        Assert.That(a.IsSuccess && b.IsSuccess, Is.True);
        Assert.That(a.Data!.MessageTypeId, Is.Not.EqualTo(b.Data!.MessageTypeId));
        Assert.That(a.Data.ThreadTypeId, Is.Not.EqualTo(b.Data.ThreadTypeId));
        Assert.That(a.Data.ReactionTypes.Select(x => x.Id).Intersect(b.Data.ReactionTypes.Select(x => x.Id)), Is.Empty);
        var read = await Service(first, first.TenantId).GetAsync(new GetChatReferenceDataRequest());
        Assert.That(read.Data!.ThreadTypeId, Is.EqualTo(a.Data.ThreadTypeId));
        Assert.That(read.Data.ReactionTypes.All(x => !string.IsNullOrWhiteSpace(x.Emoji)), Is.True);
    }

    [Test]
    public async Task GetAsync_UninitializedTenant_IsReadOnly()
    {
        var tenant = Guid.NewGuid();
        await using var db = CreateDb(tenant);
        var result = await Service(db, tenant).GetAsync(new GetChatReferenceDataRequest());
        Assert.That(result.StatusCode, Is.EqualTo(404));
        Assert.That(await db.Set<MessageType>().CountAsync(), Is.Zero);
    }

    [Test]
    public async Task EnsureAsync_UnauthorizedCaller_DoesNotWrite()
    {
        var tenant = Guid.NewGuid();
        await using var db = CreateDb(tenant);
        var service = new ChatReferenceDataService(db, new Resolver(tenant, denied: true),
            NullLogger<ChatReferenceDataService>.Instance);
        var result = await service.EnsureAsync(new EnsureChatDefaultsRequest());
        Assert.That(result.StatusCode, Is.EqualTo(403));
        Assert.That(await db.Set<MessageType>().CountAsync(), Is.Zero);
    }

    [Test]
    public async Task ReactionSummaryReader_AggregatesVisiblePageAndReturnsOnlyCallersReactionId()
    {
        var tenant = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var message = Guid.NewGuid();
        await using var db = CreateDb(tenant);
        var defaults = await Service(db, tenant).EnsureAsync(new EnsureChatDefaultsRequest());
        var typeId = defaults.Data!.ReactionTypes[0].Id;
        var own = new MessageThreadMember { Id = Guid.NewGuid(), TenantId = tenant, CredentialId = actor,
            Alias = "", Description = "", Emoji = "", IsEnabled = true };
        var other = new MessageThreadMember { Id = Guid.NewGuid(), TenantId = tenant, CredentialId = Guid.NewGuid(),
            Alias = "", Description = "", Emoji = "", IsEnabled = true };
        var ownReactionId = Guid.NewGuid();
        db.AddRange(own, other,
            new MessageReaction { Id = ownReactionId, TenantId = tenant, MessageId = message,
                TypeId = typeId, MessageThreadMemberId = own.Id, IsEnabled = true },
            new MessageReaction { Id = Guid.NewGuid(), TenantId = tenant, MessageId = message,
                TypeId = typeId, MessageThreadMemberId = other.Id, IsEnabled = true },
            new MessageReaction { Id = Guid.NewGuid(), TenantId = tenant, MessageId = Guid.NewGuid(),
                TypeId = typeId, MessageThreadMemberId = other.Id, IsEnabled = true });
        await db.SaveChangesAsync();
        var result = await new MessageReactionSummaryReader(db).ReadAsync(tenant, actor, [message], CancellationToken.None);
        Assert.That(result.Count, Is.EqualTo(1));
        var summary = result[message].Single();
        Assert.That(summary.Count, Is.EqualTo(2));
        Assert.That(summary.MyReactionId, Is.EqualTo(ownReactionId));
        Assert.That(summary.Emoji, Is.EqualTo(defaults.Data.ReactionTypes[0].Emoji));
        var foreign = await new MessageReactionSummaryReader(db).ReadAsync(Guid.NewGuid(), actor, [message], CancellationToken.None);
        Assert.That(foreign, Is.Empty);
    }

    private ChatDb CreateDb(Guid tenant) => new(
        new DbContextOptionsBuilder<ChatDb>().UseNpgsql(_postgres.GetConnectionString())
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking).Options, tenant);

    private static ChatReferenceDataService Service(ChatDb db, Guid tenant) =>
        new(db, new Resolver(tenant), NullLogger<ChatReferenceDataService>.Instance);

    private sealed class TenantAccessor(Guid tenant) : IEffectiveTenantContextAccessor
    {
        public bool HasTrustedInvocation => true;
        public Guid? EffectiveTenantId => tenant;
    }

    private sealed class ChatDb(DbContextOptions<ChatDb> options, Guid tenant) :
        XDbContext(options, new HttpContextAccessor(), new ConfigurationBuilder().Build(), new TenantAccessor(tenant))
    {
        public Guid TenantId => tenant;
        protected override void OnModelCreating(ModelBuilder model)
        {
            model.Entity<MessageType>().Ignore(x => x.MessageDirects);
            model.Entity<MessageThreadType>().Ignore(x => x.MessageThreads);
            model.Entity<MessageReactionType>().Ignore(x => x.MessageReactions);
            model.Entity<MessageDeliveryType>().Ignore(x => x.MessageDeliveries);
            // Minimal relational model for the aggregate reader: unrelated thread/Identity
            // workflows have their own integration fixtures; these queries need only scalar FKs.
            model.Entity<MessageReaction>().Ignore(x => x.Type).Ignore(x => x.Message).Ignore(x => x.MessageThreadMember);
            model.Entity<MessageThreadMember>().Ignore(x => x.Group).Ignore(x => x.Credential)
                .Ignore(x => x.MessageDeliveries).Ignore(x => x.MessageThread)
                .Ignore(x => x.MessageThreadMemberRoles).Ignore(x => x.Messages);
            model.ApplyConfiguration(new MessageTypeConfiguration());
            model.ApplyConfiguration(new MessageThreadTypeConfiguration());
            model.ApplyConfiguration(new MessageReactionTypeConfiguration());
            model.ApplyConfiguration(new MessageDeliveryTypeConfiguration());
            var testedTypes = new[] { typeof(MessageType), typeof(MessageThreadType), typeof(MessageReactionType),
                typeof(MessageDeliveryType), typeof(MessageReaction), typeof(MessageThreadMember) };
            foreach (var unrelated in model.Model.GetEntityTypes().Select(x => x.ClrType).Where(x => !testedTypes.Contains(x)).ToArray())
                model.Ignore(unrelated);
            base.OnModelCreating(model);
        }
    }

    private sealed class Resolver(Guid tenant, bool denied = false) : ICommunicationsRequestContextResolver
    {
        public Task<Result<CommunicationsRequestContext>> ResolveAsync(RequestMetadata? metadata, CancellationToken ct = default) =>
            Task.FromResult(denied ? Result<CommunicationsRequestContext>.Forbidden("Denied")
                : Result<CommunicationsRequestContext>.Success(new(Guid.NewGuid(), tenant)));
        public Task<Result<CommunicationsTenantContext>> ResolveTenantAsync(RequestMetadata? metadata, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<Result<CommunicationsTenantContext>> ResolveAdminAsync(RequestMetadata? metadata, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<Result<CommunicationsTenantContext>> ResolveTrustedInternalAsync(RequestMetadata? metadata,
            IReadOnlyCollection<string>? allowedServiceNames = null, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }
}
