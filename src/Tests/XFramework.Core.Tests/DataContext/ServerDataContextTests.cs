using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using XFramework.Core.DataContext;

namespace XFramework.Core.Tests.DataContext;

[TestFixture]
public sealed class ServerDataContextTests
{
    [Test]
    public async Task SaveChangesAsync_ConcurrencyConflict_ReturnsSafeConflict()
    {
        await using var db = new ThrowingDbContext(new DbUpdateConcurrencyException("internal concurrency detail"));
        var result = await new ServerDataContext<ThrowingDbContext>(db).SaveChangesAsync();

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(409);
        result.Message.Should().NotContain("internal concurrency detail");
    }

    [Test]
    public async Task SaveChangesAsync_DatabaseFailure_DoesNotExposeInternalDetails()
    {
        await using var db = new ThrowingDbContext(new DbUpdateException("sensitive SQL and schema detail"));
        var result = await new ServerDataContext<ThrowingDbContext>(db).SaveChangesAsync();

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        result.Message.Should().Be("The database update could not be completed.");
    }

    private sealed class ThrowingDbContext(Exception exception) : DbContext
    {
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            Task.FromException<int>(exception);
    }
}
