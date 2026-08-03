using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using IdentityServer.Domain.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using XFramework.Core.DataContext;
using XFramework.Core.Services;
using XFramework.Domain.Contexts;

namespace XFramework.Core.Tests.Services;

[TestFixture]
public sealed class TenantResolverTests
{
    [Test]
    public async Task GetTenant_MissingTenant_ThrowsInvalidOperationException()
    {
        await using var db = CreateDbContext();
        var resolver = CreateResolver(db);

        Func<Task> act = () => resolver.GetTenant(Guid.NewGuid());

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*could not be found*");
    }

    [Test]
    public async Task GetTenant_ExistingTenant_ReturnsTenant()
    {
        await using var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        db.Set<Tenant>().Add(new Tenant
        {
            Id = tenantId,
            TenantId = tenantId,
            Name = "Database tenant",
            IsEnabled = true
        });
        await db.SaveChangesAsync();

        var result = await CreateResolver(db).GetTenant(tenantId);

        result.Id.Should().Be(tenantId);
        result.Name.Should().Be("Database tenant");
    }

    [TestCase(false, false)]
    [TestCase(true, true)]
    public async Task GetTenant_UnavailableTenant_IsRejected(bool isEnabled, bool isDeleted)
    {
        await using var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        db.Set<Tenant>().Add(new Tenant
        {
            Id = tenantId,
            TenantId = tenantId,
            Name = "Unavailable tenant",
            IsEnabled = isEnabled,
            IsDeleted = isDeleted
        });
        await db.SaveChangesAsync();

        Func<Task> act = () => CreateResolver(db).GetTenant(tenantId);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*could not be found*");
    }

    [Test]
    public async Task GetTenant_EmptyTenantId_ThrowsArgumentNullException()
    {
        await using var db = CreateDbContext();

        Func<Task> act = () => CreateResolver(db).GetTenant(Guid.Empty);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Test]
    public async Task GetTenant_CanceledQuery_PropagatesCancellation()
    {
        await using var db = CreateDbContext();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        Func<Task> act = () => CreateResolver(db).GetTenant(Guid.NewGuid(), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private static TenantResolver CreateResolver(AppDbContext db) =>
        new(new ServerDataContext<AppDbContext>(db));

    private static AppDbContext CreateDbContext()
    {
        _ = typeof(Tenant);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new AppDbContext(options);
    }
}
