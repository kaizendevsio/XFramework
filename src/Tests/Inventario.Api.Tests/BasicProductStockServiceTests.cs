using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using XFramework.Core.Patterns;
using XFramework.Core.Services.FeatureGates;
using XFramework.Integration.Security;
using XFramework.Inventario.Api.Services;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Stock;

namespace Inventario.Api.Tests;

[TestFixture]
public sealed class BasicProductStockServiceTests
{
    [TestCase("actor", 401)]
    [TestCase("expired", 401)]
    [TestCase("tenant", 401)]
    [TestCase("metadata", 403)]
    [TestCase("capability", 403)]
    [TestCase("delegation", 403)]
    public async Task PostAsync_UntrustedOrUnauthorizedRequest_RejectsBeforeDatabaseAccess(string reason, int status)
    {
        var tenant = Guid.NewGuid();
        var actor = new TrustedActorIdentity(Guid.NewGuid(), null, reason == "delegation" ? Guid.NewGuid() : tenant,
            Guid.NewGuid(), new HashSet<string>(), reason == "capability" ? new HashSet<string>() : new HashSet<string> { "inventario.movements:create" },
            "test", DateTimeOffset.UtcNow.AddHours(reason == "expired" ? -1 : 1));
        var invocation = new Mock<ITrustedInvocationContextAccessor>();
        invocation.SetupGet(x => x.Current).Returns(new TrustedInvocationContext(reason == "actor" ? null : actor, null,
            reason == "tenant" ? null : tenant, null, Guid.NewGuid()));
        var features = new Mock<ITenantModuleFeatureService>(MockBehavior.Strict);
        // No provider: reaching transaction/query code would fail instead of returning the expected denial.
        using var db = new DbContext(new DbContextOptionsBuilder().Options);
        var service = new BasicProductStockService(db, invocation.Object, features.Object, null!, NullLogger<BasicProductStockService>.Instance);
        var request = new PostBasicProductStockRequest();
        if (reason == "metadata") request.Metadata.RequestedTenantId = Guid.NewGuid();
        var result = await service.PostAsync(request);
        result.StatusCode.Should().Be(status);
        features.VerifyNoOtherCalls();
    }

    [TestCase("root", 403)]
    [TestCase("stock_balances", 403)]
    [TestCase("movements", 403)]
    [TestCase("warehousing", 403)]
    [TestCase("invalid_quantity", 400)]
    public async Task PostAsync_DisabledFeatureOrInvalidShape_RejectsBeforeDatabaseAccess(string reason, int status)
    {
        var tenant = Guid.NewGuid();
        var actor = new TrustedActorIdentity(Guid.NewGuid(), null, tenant, Guid.NewGuid(), new HashSet<string>(),
            new HashSet<string> { "inventario.movements:create" }, "test", DateTimeOffset.UtcNow.AddHours(1));
        var invocation = new Mock<ITrustedInvocationContextAccessor>();
        invocation.SetupGet(x => x.Current).Returns(new TrustedInvocationContext(actor, null, tenant, null, Guid.NewGuid()));
        var features = new Mock<ITenantModuleFeatureService>();
        features.Setup(x => x.EnsureEnabledAsync(tenant, "inventario", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, string _, string? subFeature, CancellationToken _) =>
                (subFeature ?? "root") == reason ? Result.Forbidden("Feature disabled.") : Result.Success());
        using var db = new DbContext(new DbContextOptionsBuilder().Options);
        var service = new BasicProductStockService(db, invocation.Object, features.Object, null!, NullLogger<BasicProductStockService>.Instance);
        var result = await service.PostAsync(new() { ProductId = Guid.NewGuid(), Quantity = 0, IdempotencyKey = "receipt" });
        result.StatusCode.Should().Be(status);
    }
}
