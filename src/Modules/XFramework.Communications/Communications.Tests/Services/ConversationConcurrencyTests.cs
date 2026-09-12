using Communications.Domain.Shared.Contracts;
using Communications.Domain.Shared.Contracts.Requests.Threads;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Testcontainers.PostgreSql;
using XFramework.Core.DataContext;

namespace Communications.Tests.Services;

public sealed partial class ThreadServiceSecurityTests
{
    [Test, Category("Kind:Integration")]
    public async Task DeleteConversation_Postgres_PersistsRevocationAndPrivateTombstones()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
        await postgres.StartAsync();
        var options = new DbContextOptionsBuilder<MembershipDb>().UseNpgsql(postgres.GetConnectionString())
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking).Options;
        var tenant = Guid.NewGuid(); var thread = Thread(Guid.NewGuid(), tenant);
        var admin = Member(Guid.NewGuid(), thread.Id, Guid.NewGuid(), tenant); admin.Role = "Admin";
        var recipient = Member(Guid.NewGuid(), thread.Id, Guid.NewGuid(), tenant);
        await using (var seed = new MembershipDb(options))
        {
            await seed.Database.EnsureCreatedAsync(); seed.AddRange(thread, admin, recipient); await seed.SaveChangesAsync();
        }
        await using (var db = new MembershipDb(options))
        {
            var result = await CreateService(new ServerDataContext<MembershipDb>(db), database: db)
                .DeleteThreadAsync(new() { ThreadId = thread.Id, Metadata = Metadata(admin.CredentialId, tenant) });
            Assert.That(result.IsSuccess, Is.True, result.Message);
        }
        await using var check = new MembershipDb(options);
        Assert.That(await check.Set<MessageThreadMember>().CountAsync(), Is.Zero, "Normal queries hide revoked memberships.");
        Assert.That(await check.Set<MessageThread>().CountAsync(), Is.Zero);
        Assert.That(await check.Set<MessageOutboxEvent>().CountAsync(), Is.EqualTo(1));
        var service = CreateService(new ServerDataContext<MembershipDb>(check), database: check);
        var deleted = await service.GetDeletedThreadsAsync(new() { Metadata = Metadata(recipient.CredentialId, tenant) });
        Assert.That(deleted.IsSuccess, Is.True, deleted.Message);
        Assert.That(deleted.Data!.Items, Is.EqualTo(new[] { thread.Id }));
        Assert.That((await service.GetDeletedThreadsAsync(new() { Metadata = Metadata(recipient.CredentialId, Guid.NewGuid()) })).Data!.Items, Is.Empty);
    }

    [Test, Category("Kind:Integration")]
    public async Task MemberRoles_ConcurrentDemotionsAcrossConnections_PreserveOneAdmin()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
        await postgres.StartAsync();
        var options = new DbContextOptionsBuilder<MembershipDb>().UseNpgsql(postgres.GetConnectionString())
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking).Options;
        var tenant = Guid.NewGuid(); var thread = Guid.NewGuid();
        var first = Member(Guid.NewGuid(), thread, Guid.NewGuid(), tenant); first.Role = "Admin";
        var second = Member(Guid.NewGuid(), thread, Guid.NewGuid(), tenant); second.Role = "Admin";
        await using (var seed = new MembershipDb(options)) { await seed.Database.EnsureCreatedAsync(); seed.AddRange(first, second); await seed.SaveChangesAsync(); }
        var results = await Task.WhenAll(new[] { first, second }.Select(async member => {
            await using var db = new MembershipDb(options);
            var service = CreateService(new ServerDataContext<MembershipDb>(db), database: db);
            return await service.UpdateThreadMemberRoleAsync(new UpdateThreadMemberRoleRequest {
                ThreadId = thread, MemberId = member.Id, Role = "Member", Metadata = Metadata(member.CredentialId, tenant)
            });
        }));
        Assert.That(results.Count(r => r.IsSuccess), Is.EqualTo(1), string.Join("; ", results.Select(r => r.Message)));
        Assert.That(results.Count(r => r.StatusCode == 400), Is.EqualTo(1));
        await using var check = new MembershipDb(options);
        Assert.That(await check.Set<MessageThreadMember>().CountAsync(m => m.Role == "Admin"), Is.EqualTo(1));
        Assert.That(await check.Set<MessageOutboxEvent>().CountAsync(), Is.EqualTo(1));
    }

    private sealed class MembershipDb(DbContextOptions<MembershipDb> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder model)
        {
            model.Entity<MessageThreadMember>().Ignore(x => x.Group).Ignore(x => x.Credential)
                .Ignore(x => x.MessageDeliveries).Ignore(x => x.MessageThread)
                .Ignore(x => x.MessageThreadMemberRoles).Ignore(x => x.Messages);
            model.Entity<MessageOutboxEvent>();
            model.Entity<MessageThreadMemberRole>().Ignore(x => x.MessageThreadMember).Ignore(x => x.Role);
            model.Entity<MessageThread>().Ignore(x => x.Type).Ignore(x => x.MessageThreadMemberGroups)
                .Ignore(x => x.MessageThreadMembers).Ignore(x => x.Messages).HasQueryFilter(x => !x.IsDeleted);
            model.Entity<MessageDirectThread>();
            model.Entity<MessageThreadMember>().HasQueryFilter(x => !x.IsDeleted);
        }
    }
}
