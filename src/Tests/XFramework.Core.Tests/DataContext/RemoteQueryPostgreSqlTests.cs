using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using FluentAssertions;
using IdentityServer.Domain.Shared.Contracts;
using MemoryPack;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Testcontainers.PostgreSql;
using XFramework.Core.DataContext;
using XFramework.Domain.Shared.Contracts.Base;
using XFramework.Domain.Shared.DataContext;
using XFramework.Domain.Shared.Enums;
using XFramework.Integration.DataContext.ExpressionVisitor;

namespace XFramework.Core.Tests.DataContext;

[TestFixture]
public sealed class RemoteQueryPostgreSqlTests
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
    private DbContextOptions<QueryDb> options = null!;

    [OneTimeSetUp]
    public async Task StartDatabase()
    {
        await postgres.StartAsync();
        options = new DbContextOptionsBuilder<QueryDb>().UseNpgsql(postgres.GetConnectionString()).Options;
        await using var db = new QueryDb(options);
        await db.Database.EnsureCreatedAsync();
    }

    [OneTimeTearDown]
    public async Task StopDatabase() => await postgres.DisposeAsync();

    [Test]
    public async Task Execute_PortalRoleCredentialArray_ReturnsOnlyAssignedTenantRoles()
    {
        await using var db = new QueryDb(options);
        var tenant = Guid.NewGuid();
        var credentialIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var assigned = new IdentityRole { Id = Guid.NewGuid(), TenantId = tenant, CredentialId = credentialIds[0], IsEnabled = true };
        db.AddRange(assigned,
            new IdentityRole { Id = Guid.NewGuid(), TenantId = tenant, CredentialId = Guid.NewGuid(), IsEnabled = true },
            new IdentityRole { Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), CredentialId = credentialIds[0], IsEnabled = true },
            new IdentityRole { Id = Guid.NewGuid(), TenantId = tenant, CredentialId = credentialIds[1], IsDeleted = true });
        await db.SaveChangesAsync();

        var descriptor = RoundTrip<IdentityRole>(role => credentialIds.Contains(role.CredentialId));
        descriptor.Take = 1000;
        QueryDescriptorExecutor.ValidateDescriptor(descriptor).Should().BeNull();
        var rows = (List<IdentityRole>)(await QueryDescriptorExecutor.ExecuteAsync(db, typeof(IdentityRole), descriptor, tenant))!;
        rows.Should().ContainSingle().Which.Id.Should().Be(assigned.Id);
    }

    [Test]
    public async Task Execute_NestedAndOrAndIgnoreCase_PreservesGroupingTenantAndLiteralSearch()
    {
        await using var db = new QueryDb(options);
        var tenant = Guid.NewGuid();
        var rows = new[]
        {
            Row(tenant, "alice", "Alice", true),
            Row(tenant, "other", "ALICE", false),
            Row(tenant, "unrelated", "Other", true),
            Row(tenant, null, "ALICE", true),
            Row(Guid.NewGuid(), "alice", "ALICE", true),
            Row(tenant, "percent", "100%_\\match", true),
            Row(tenant, "wildcard", "100abcXmatch", true)
        };
        db.AddRange(rows);
        await db.SaveChangesAsync();
        Expression<Func<SearchRow, bool>> predicate = x =>
            (x.UserName != null && x.UserName.Contains("alice", StringComparison.OrdinalIgnoreCase)) ||
            (x.IsEnabled && x.UserAlias != null && x.UserAlias.Contains("alice", StringComparison.OrdinalIgnoreCase));
        var found = await Execute(db, tenant, predicate);
        found.Select(x => x.Id).Should().BeEquivalentTo(new[] { rows[0].Id, rows[3].Id });

        found = await Execute(db, tenant, x => x.UserName == "alice" || x.UserAlias == "ALICE");
        found.Select(x => x.Id).Should().BeEquivalentTo(new[] { rows[0].Id, rows[1].Id, rows[3].Id });

        found = await Execute(db, tenant, x => x.UserAlias!.Contains("%_\\", StringComparison.OrdinalIgnoreCase));
        found.Should().ContainSingle().Which.Id.Should().Be(rows[5].Id);
        found = await Execute(db, tenant, x => x.UserAlias!.Contains("alice"));
        found.Should().BeEmpty("the existing overload remains case-sensitive");
    }

    [Test]
    public async Task Execute_EmptyAndIntersectedMembership_DoesNotBroadenQuery()
    {
        await using var db = new QueryDb(options);
        var tenant = Guid.NewGuid();
        var first = Row(tenant, "first", null, true);
        var second = Row(tenant, "second", null, true);
        db.AddRange(first, second);
        await db.SaveChangesAsync();
        Guid[] empty = [];
        (await Execute(db, tenant, x => empty.Contains(x.Id))).Should().BeEmpty();
        var both = new[] { first.Id, second.Id };
        var onlySecond = new List<Guid> { second.Id };
        (await Execute(db, tenant, x => both.Contains(x.Id) && onlySecond.Contains(x.Id)))
            .Should().ContainSingle().Which.Id.Should().Be(second.Id);
        (await Execute(db, tenant, x => empty.Contains(x.Id) || x.Id == first.Id))
            .Should().ContainSingle().Which.Id.Should().Be(first.Id);
    }

    private static QueryDescriptor RoundTrip<T>(Expression<Func<T, bool>> predicate) =>
        MemoryPackSerializer.Deserialize<QueryDescriptor>(MemoryPackSerializer.Serialize(new QueryDescriptor
        {
            EntityTypeName = typeof(T).Name, Filters = QueryExpressionVisitor.Parse(predicate),
            Mode = QueryExecutionMode.ToList, Take = 100
        }))!;

    private static async Task<List<SearchRow>> Execute(QueryDb db, Guid tenant, Expression<Func<SearchRow, bool>> predicate) =>
        (List<SearchRow>)(await QueryDescriptorExecutor.ExecuteAsync(db, typeof(SearchRow), RoundTrip(predicate), tenant))!;

    private static SearchRow Row(Guid tenant, string? userName, string? alias, bool enabled) =>
        new() { Id = Guid.NewGuid(), TenantId = tenant, UserName = userName, UserAlias = alias, IsEnabled = enabled };

    public sealed class SearchRow : IHasTenantId
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string? UserName { get; set; }
        public string? UserAlias { get; set; }
        public bool IsEnabled { get; set; }
    }

    private sealed class QueryDb(DbContextOptions<QueryDb> dbOptions) : DbContext(dbOptions)
    {
        protected override void OnModelCreating(ModelBuilder model)
        {
            model.Entity<SearchRow>().HasKey(x => x.Id);
            var role = model.Entity<IdentityRole>();
            role.HasKey(x => x.Id);
            role.Ignore(x => x.Type);
            role.Ignore(x => x.Credential);
            role.Ignore(x => x.PermissionOverrides);
            foreach (var entity in model.Model.GetEntityTypes().ToArray())
                if (entity.ClrType != typeof(SearchRow) && entity.ClrType != typeof(IdentityRole))
                    model.Ignore(entity.ClrType);
        }
    }
}
