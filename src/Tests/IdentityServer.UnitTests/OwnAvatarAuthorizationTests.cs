using IdentityServer.Api.Services;
using IdentityServer.Domain.Shared.Contracts.Requests;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using Storage.Integration.Drivers;
using XFramework.Core.Patterns;
using XFramework.Core.RateLimiting;
using XFramework.Core.Services;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.DataContext;
using XFramework.Integration.Abstractions;
using XFramework.Integration.Security;
using XFramework.Integration.Services;

namespace IdentityServer.UnitTests;

public sealed class OwnAvatarAuthorizationTests
{
    [TestCase(true)]
    [TestCase(false)]
    public async Task OwnPhoto_DerivesTargetFromTrustedActor_AndCannotBypassAuthorization(bool signedIn)
    {
        var tenant = Guid.NewGuid(); var credential = Guid.NewGuid();
        var actor = signedIn ? new TrustedActorIdentity(credential, null, tenant, Guid.NewGuid(), new HashSet<string>(), new HashSet<string>(), "test", DateTimeOffset.UtcNow.AddMinutes(5)) : null;
        var trusted = new TestTrustedInvocationContextAccessor(new(actor, null, tenant, tenant, Guid.NewGuid()));
        var authorization = new Mock<IIdentityAuthorizationService>(MockBehavior.Strict);
        authorization.Setup(x => x.AuthorizeCredentialOperationAsync(It.IsAny<RequestMetadata>(), tenant, credential, It.IsAny<string>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure("Not authorized", 403));
        using var db = new DbContext(new DbContextOptionsBuilder<DbContext>().Options);
        using var memory = new MemoryCache(new MemoryCacheOptions());
        var service = new AuthService(Mock.Of<IDataContext>(), db, Mock.Of<IServiceScopeFactory>(), new HttpContextAccessor(),
            Mock.Of<ITenantResolver>(), Mock.Of<IJwtService>(), TimeProvider.System, Mock.Of<IDistributedSecurityRateLimiter>(), trusted,
            new CacheManager(memory, NullLogger<CacheManager>.Instance), Mock.Of<IStorageServiceWrapper>(), authorization.Object, NullLogger<AuthService>.Instance);
        var result = await service.UploadOwnAvatarAsync(new UploadOwnAvatarRequest { Metadata = new() { RequestedTenantId = Guid.NewGuid() },
            FileName = "photo.jpg", ContentType = "image/jpeg", FileBytes = [0xff, 0xd8, 0xff, 0xe0] });
        Assert.That(result.StatusCode, Is.EqualTo(signedIn ? 403 : 401));
        authorization.Verify(x => x.AuthorizeCredentialOperationAsync(It.IsAny<RequestMetadata>(), tenant, credential, It.IsAny<string>(), true, It.IsAny<CancellationToken>()), signedIn ? Times.Once() : Times.Never());
    }
}
