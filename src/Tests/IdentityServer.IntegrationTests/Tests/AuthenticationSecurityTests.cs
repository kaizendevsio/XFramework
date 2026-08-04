using System.Net;
using System.Text;
using System.Text.Json;
using IdentityServer.Api.Services;
using IdentityServer.Domain.Shared;
using IdentityServer.Domain.Shared.Contracts;
using IdentityServer.Domain.Shared.Contracts.Requests;
using IdentityServer.Domain.Shared.Contracts.Responses;
using MemoryPack;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using XFramework.Core.Patterns;
using XFramework.Core.RateLimiting;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.Enums;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Security;
using Session = IdentityServer.Domain.Shared.Contracts.Session;

namespace IdentityServer.IntegrationTests.Tests;

[TestFixture]
[NonParallelizable]
public sealed class AuthenticationSecurityTests : IntegrationTestBase
{
    private IDisposable? _actorAccessTokenSuppression;

    [SetUp]
    public void SuppressAmbientActorAccessToken() =>
        _actorAccessTokenSuppression = IntegrationTestFixture.SuppressActorAccessToken();

    [TearDown]
    public void ResetTestState()
    {
        _actorAccessTokenSuppression?.Dispose();
        _actorAccessTokenSuppression = null;
        IntegrationTestFixture.Services.GetRequiredService<TestDistributedSecurityRateLimiter>().Reset();
    }

    [Test]
    public async Task Authenticate_BusinessPath_UsesCompositeDistributedLimitAndFailsClosed()
    {
        var limiter = IntegrationTestFixture.Services
            .GetRequiredService<TestDistributedSecurityRateLimiter>();
        limiter.Reset(DistributedSecurityRateLimitDecision.Rejected(TimeSpan.FromSeconds(30)));
        var request = CreateAuthRequest(
            IntegrationTestFixture.TestTenantId,
            TestData.RoleTypeId,
            "  Limited.User@Example.Test ",
            "unused-password");
        request.Metadata.IpAddress = "198.51.100.77";

        try
        {
            var result = await AuthenticateDirect(request, IPAddress.Parse("::ffff:192.0.2.25"));

            result.StatusCode.Should().Be(429);
            result.Message.Should().Be("Too many requests.");
            limiter.Calls.Should().ContainSingle();
            var call = limiter.Calls.Single();
            call.Policy.Should().Be(StrictSecurityRateLimitPolicyMap.Authentication);
            call.ClientKey.Should().Be(
                StrictSecurityRateLimitPolicyMap.CreateAuthenticationClientKey(
                    "192.0.2.25",
                    "limited.user@example.test"));
            call.ClientKey.Should().NotContain("LIMITED");
            call.ClientKey.Should().NotContain("192.0.2.25");
        }
        finally
        {
            limiter.Reset();
        }
    }

    [Test]
    public async Task ForgotPassword_HttpAndBolt_EachChargeSharedDistributedLimitOnce()
    {
        var limiter = IntegrationTestFixture.Services
            .GetRequiredService<TestDistributedSecurityRateLimiter>();
        var request = new ForgotPasswordRequest
        {
            Email = $"unknown-{Guid.NewGuid():N}@example.test",
            Metadata = new RequestMetadata
            {
                RequestedTenantId = IntegrationTestFixture.TestTenantId,
                IpAddress = "198.51.100.20"
            }
        };

        var boltResult = await IntegrationTestFixture.ServiceWrapper.ForgotPassword(request);

        boltResult.HttpStatusCode.Should().Be(HttpStatusCode.OK);
        limiter.Calls.Should().ContainSingle();
        limiter.Calls.Single().Policy.Should().Be(StrictSecurityRateLimitPolicyMap.PasswordReset);

        limiter.Reset();
        using var response = await HttpClient.PostAsJsonAsync("/api/auth/forgot-password", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        limiter.Calls.Should().ContainSingle("the HTTP path must not be charged again by middleware");
        limiter.Calls.Single().Policy.Should().Be(StrictSecurityRateLimitPolicyMap.PasswordReset);
    }

    [Test]
    public async Task ResetPassword_Bolt_WhenDistributedLimiterFails_IsDeniedBeforeTokenLookup()
    {
        var limiter = IntegrationTestFixture.Services
            .GetRequiredService<TestDistributedSecurityRateLimiter>();
        limiter.ResetWithException(new InvalidOperationException("injected limiter outage"));

        var result = await IntegrationTestFixture.ServiceWrapper.ResetPassword(new ResetPasswordRequest
        {
            Token = Guid.NewGuid().ToString("N"),
            NewPassword = "ValidPassword123!",
            Metadata = new RequestMetadata
            {
                RequestedTenantId = IntegrationTestFixture.TestTenantId,
                IpAddress = "198.51.100.21"
            }
        });

        result.HttpStatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        limiter.Calls.Should().ContainSingle();
        limiter.Calls.Single().Policy.Should().Be(StrictSecurityRateLimitPolicyMap.PasswordReset);
    }

    [Test]
    public async Task CreateVerification_Http_WhenDistributedLimitIsExceeded_IsDeniedBeforePersistence()
    {
        var limiter = IntegrationTestFixture.Services
            .GetRequiredService<TestDistributedSecurityRateLimiter>();
        limiter.Reset(DistributedSecurityRateLimitDecision.Rejected(TimeSpan.FromMinutes(1)));

        using var response = await HttpClient.PostAsJsonAsync(
            "/api/verifications",
            new Create<IdentityVerification>(new IdentityVerification
            {
                CredentialId = Guid.NewGuid(),
                VerificationTypeId = IdentityConstants.VerificationType.Email,
                TenantId = IntegrationTestFixture.TestTenantId
            })
            {
                Metadata = new RequestMetadata
                {
                    IpAddress = "198.51.100.22"
                }
            });

        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        limiter.Calls.Should().ContainSingle();
        limiter.Calls.Single().Policy.Should().Be(StrictSecurityRateLimitPolicyMap.Verification);
    }

    [Test]
    public async Task Authenticate_ConcurrentFailures_AreSerializedAndLockedResponseIsGeneric()
    {
        IntegrationTestFixture.Services.GetRequiredService<TestDistributedSecurityRateLimiter>().Reset();
        var seeded = await SeedAuthenticationGraph();

        var attempts = Enumerable.Range(0, 8)
            .Select(_ => AuthenticateDirect(CreateAuthRequest(
                seeded.TenantId,
                seeded.RoleTypeId,
                seeded.Username,
                "definitely-wrong-password")))
            .ToArray();
        var results = await Task.WhenAll(attempts);

        results.Should().OnlyContain(result =>
            result.StatusCode == 401 && result.Message == "Invalid credentials");

        await using (var db = CreateDbContext())
        {
            var credential = await db.Set<IdentityCredential>()
                .IgnoreQueryFilters()
                .SingleAsync(item => item.Id == seeded.CredentialId);
            credential.FailedLoginAttempts.Should().Be(5);
            credential.LockoutEnd.Should().BeAfter(DateTime.UtcNow);
        }

        var unknown = await AuthenticateDirect(CreateAuthRequest(
            seeded.TenantId,
            seeded.RoleTypeId,
            UniqueUsername(),
            "definitely-wrong-password"));
        var lockedWithCorrectPassword = await AuthenticateDirect(CreateAuthRequest(
            seeded.TenantId,
            seeded.RoleTypeId,
            seeded.Username,
            seeded.Password));

        lockedWithCorrectPassword.StatusCode.Should().Be(unknown.StatusCode).And.Be(401);
        lockedWithCorrectPassword.Message.Should().Be(unknown.Message).And.Be("Invalid credentials");
        await AssertNoSessionExists(seeded.CredentialId);
    }

    [TestCase(false, false, TestName = "Bolt_Authenticate_DisabledTenant_IsRejected")]
    [TestCase(true, true, TestName = "Bolt_Authenticate_DeletedTenant_IsRejected")]
    public async Task Bolt_Authenticate_WithUnavailableTenant_IsRejected(
        bool isDeleted,
        bool isEnabled)
    {
        var seeded = await SeedAuthenticationGraph(
            tenantIsEnabled: isEnabled,
            tenantIsDeleted: isDeleted);

        var result = await IntegrationTestFixture.ServiceWrapper.AuthenticateIdentity(
            CreateAuthRequest(seeded.TenantId, seeded.RoleTypeId, seeded.Username, seeded.Password));

        result.HttpStatusCode.Should().NotBe(HttpStatusCode.OK);
        result.Response.Should().BeNull();
        await AssertNoSessionExists(seeded.CredentialId);
    }

    [TestCase(false, false, TestName = "Bolt_Authenticate_DisabledCredential_IsRejected")]
    [TestCase(true, true, TestName = "Bolt_Authenticate_DeletedCredential_IsRejected")]
    public async Task Bolt_Authenticate_WithUnavailableCredential_IsRejected(
        bool isDeleted,
        bool isEnabled)
    {
        var seeded = await SeedAuthenticationGraph(
            credentialIsEnabled: isEnabled,
            credentialIsDeleted: isDeleted);

        var result = await IntegrationTestFixture.ServiceWrapper.AuthenticateIdentity(
            CreateAuthRequest(seeded.TenantId, seeded.RoleTypeId, seeded.Username, seeded.Password));

        result.HttpStatusCode.Should().Be(HttpStatusCode.Unauthorized);
        result.Response.Should().BeNull();
        await AssertNoSessionExists(seeded.CredentialId);
    }

    [Test]
    public async Task Bolt_Authenticate_WithTokenAuthorizationType_IsRejected()
    {
        var request = CreateAuthRequest(
            IntegrationTestFixture.TestTenantId,
            TestData.RoleTypeId,
            UniqueUsername(),
            "unused-password");
        request.AuthorizationType = AuthorizationType.Token;

        var result = await IntegrationTestFixture.ServiceWrapper.AuthenticateIdentity(request);

        result.HttpStatusCode.Should().Be(HttpStatusCode.BadRequest);
        result.Response.Should().BeNull();
    }

    [TestCase(false, false, TestName = "Bolt_Authenticate_DisabledIdentity_IsRejected")]
    [TestCase(true, true, TestName = "Bolt_Authenticate_DeletedIdentity_IsRejected")]
    public async Task Bolt_Authenticate_WithUnavailableIdentity_IsRejected(bool isDeleted, bool isEnabled)
    {
        var seeded = await SeedAuthenticationGraph(
            identityIsEnabled: isEnabled,
            identityIsDeleted: isDeleted);

        var result = await IntegrationTestFixture.ServiceWrapper.AuthenticateIdentity(
            CreateAuthRequest(seeded.TenantId, seeded.RoleTypeId, seeded.Username, seeded.Password));

        result.HttpStatusCode.Should().Be(HttpStatusCode.Unauthorized);
        result.Response.Should().BeNull();
        await AssertNoSessionExists(seeded.CredentialId);
    }

    [Test]
    public async Task Bolt_Authenticate_WithDisabledRoleType_IsRejected()
    {
        var seeded = await SeedAuthenticationGraph(roleTypeIsEnabled: false);

        var result = await IntegrationTestFixture.ServiceWrapper.AuthenticateIdentity(
            CreateAuthRequest(seeded.TenantId, seeded.RoleTypeId, seeded.Username, seeded.Password));

        result.HttpStatusCode.Should().Be(HttpStatusCode.Forbidden);
        result.Response.Should().BeNull();
        await AssertNoSessionExists(seeded.CredentialId);
    }

    [Test]
    public async Task Bolt_Authenticate_WhenTokenGenerationDisabled_ReturnsNoTokensOrSession()
    {
        var seeded = await SeedAuthenticationGraph();
        var request = CreateAuthRequest(
            seeded.TenantId,
            seeded.RoleTypeId,
            seeded.Username,
            seeded.Password);
        request.GenerateToken = false;

        var result = await IntegrationTestFixture.ServiceWrapper.AuthenticateIdentity(request);

        result.HttpStatusCode.Should().Be(HttpStatusCode.OK, result.Message);
        result.Response.Should().NotBeNull();
        result.Response!.AccessToken.Should().BeNull();
        result.Response.RefreshToken.Should().BeNull();
        result.Response.SessionId.Should().BeNull();
        result.Response.ExpiresIn.Should().Be(0);
        await AssertNoSessionExists(seeded.CredentialId);
    }

    [TestCase(false, false, TestName = "Bolt_Authenticate_DisabledEmailContact_IsRejected")]
    [TestCase(true, true, TestName = "Bolt_Authenticate_DeletedEmailContact_IsRejected")]
    public async Task Bolt_Authenticate_WithUnavailableEmailContact_IsRejected(bool isDeleted, bool isEnabled)
    {
        var seeded = await SeedAuthenticationGraph();
        var email = await SeedEmailContact(seeded, isEnabled, isDeleted);
        var request = CreateAuthRequest(
            seeded.TenantId,
            seeded.RoleTypeId,
            email,
            seeded.Password,
            AuthorizationType.Email);

        var result = await IntegrationTestFixture.ServiceWrapper.AuthenticateIdentity(request);

        result.HttpStatusCode.Should().Be(HttpStatusCode.Unauthorized);
        result.Response.Should().BeNull();
        await AssertNoSessionExists(seeded.CredentialId);
    }

    [Test]
    public async Task Bolt_RefreshToken_AfterRoleTypeDisabled_RevokesSession()
    {
        var seeded = await SeedAuthenticationGraph();
        var auth = await IntegrationTestFixture.ServiceWrapper.AuthenticateIdentity(
            CreateAuthRequest(seeded.TenantId, seeded.RoleTypeId, seeded.Username, seeded.Password));
        auth.HttpStatusCode.Should().Be(HttpStatusCode.OK, auth.Message);

        await using (var db = CreateDbContext())
        {
            var roleType = await db.Set<IdentityRoleType>()
                .IgnoreQueryFilters()
                .SingleAsync(item => item.Id == seeded.RoleTypeId);
            roleType.IsEnabled = false;
            roleType.ConcurrencyStamp = Guid.NewGuid();
            await db.SaveChangesAsync();
        }

        var refresh = await IntegrationTestFixture.ServiceWrapper.RefreshToken(new RefreshTokenRequest
        {
            AccessToken = auth.Response!.AccessToken,
            RefreshToken = auth.Response.RefreshToken,
            SessionId = auth.Response.SessionId!.Value,
            Metadata = CreateAuthRequest(seeded.TenantId, seeded.RoleTypeId, seeded.Username, seeded.Password).Metadata
        });

        refresh.HttpStatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await using var verifyDb = CreateDbContext();
        var session = await verifyDb.Set<Session>()
            .IgnoreQueryFilters()
            .SingleAsync(item => item.Id == auth.Response.SessionId.Value);
        session.Status.Should().Be(CurrentSessionState.Inactive);
    }

    [Test]
    public async Task Bolt_RefreshToken_AfterRefreshExpiry_IsRejectedWhileRememberMeSessionRemainsActive()
    {
        var seeded = await SeedAuthenticationGraph();
        var request = CreateAuthRequest(
            seeded.TenantId,
            seeded.RoleTypeId,
            seeded.Username,
            seeded.Password);
        request.RememberMe = true;
        var auth = await IntegrationTestFixture.ServiceWrapper.AuthenticateIdentity(request);
        auth.HttpStatusCode.Should().Be(HttpStatusCode.OK, auth.Message);

        await using (var db = CreateDbContext())
        {
            var session = await db.Set<Session>()
                .IgnoreQueryFilters()
                .SingleAsync(item => item.Id == auth.Response!.SessionId!.Value);
            session.ExpiresAt.Should().BeAfter(DateTime.UtcNow.AddDays(20));
            session.RefreshTokenExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            session.ConcurrencyStamp = Guid.NewGuid();
            await db.SaveChangesAsync();
        }

        var refresh = await IntegrationTestFixture.ServiceWrapper.RefreshToken(new RefreshTokenRequest
        {
            AccessToken = auth.Response!.AccessToken,
            RefreshToken = auth.Response.RefreshToken,
            SessionId = auth.Response.SessionId!.Value,
            Metadata = request.Metadata
        });

        refresh.HttpStatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await using var verifyDb = CreateDbContext();
        var persisted = await verifyDb.Set<Session>()
            .IgnoreQueryFilters()
            .SingleAsync(item => item.Id == auth.Response.SessionId.Value);
        persisted.Status.Should().Be(CurrentSessionState.Active);
        persisted.ExpiresAt.Should().BeAfter(DateTime.UtcNow.AddDays(20));
        persisted.RefreshTokenHash.Should().BeNull();
    }

    [Test]
    public async Task Bolt_RefreshToken_RotatesRefreshTokenExpiration()
    {
        var seeded = await SeedAuthenticationGraph();
        var request = CreateAuthRequest(
            seeded.TenantId,
            seeded.RoleTypeId,
            seeded.Username,
            seeded.Password);
        var auth = await IntegrationTestFixture.ServiceWrapper.AuthenticateIdentity(request);
        auth.HttpStatusCode.Should().Be(HttpStatusCode.OK, auth.Message);
        var shortenedExpiry = DateTime.UtcNow.AddMinutes(1);

        await using (var db = CreateDbContext())
        {
            var session = await db.Set<Session>()
                .IgnoreQueryFilters()
                .SingleAsync(item => item.Id == auth.Response!.SessionId!.Value);
            session.RefreshTokenExpiresAt = shortenedExpiry;
            session.ConcurrencyStamp = Guid.NewGuid();
            await db.SaveChangesAsync();
        }

        var refresh = await IntegrationTestFixture.ServiceWrapper.RefreshToken(new RefreshTokenRequest
        {
            AccessToken = auth.Response!.AccessToken,
            RefreshToken = auth.Response.RefreshToken,
            SessionId = auth.Response.SessionId!.Value,
            Metadata = request.Metadata
        });

        refresh.HttpStatusCode.Should().Be(HttpStatusCode.OK, refresh.Message);
        await using var verifyDb = CreateDbContext();
        var persisted = await verifyDb.Set<Session>()
            .IgnoreQueryFilters()
            .SingleAsync(item => item.Id == auth.Response.SessionId.Value);
        persisted.RefreshTokenExpiresAt.Should().BeAfter(shortenedExpiry.AddMinutes(20));
    }

    [Test]
    public async Task LogoutAndRefresh_ConcurrentRace_AlwaysLeavesSessionInactive()
    {
        var seeded = await SeedAuthenticationGraph();
        var authRequest = CreateAuthRequest(
            seeded.TenantId,
            seeded.RoleTypeId,
            seeded.Username,
            seeded.Password);
        var auth = await AuthenticateDirect(authRequest);
        auth.IsSuccess.Should().BeTrue(auth.Message);
        var sessionId = auth.Data!.SessionId!.Value;
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var refreshTask = Task.Run(async () =>
        {
            await start.Task;
            await using var scope = IntegrationTestFixture.Services.CreateAsyncScope();
            IntegrationTestFixture.EstablishTrustedServiceTargetContext(scope.ServiceProvider, seeded.TenantId);
            return await scope.ServiceProvider.GetRequiredService<IAuthService>()
                .RefreshTokenAsync(new RefreshTokenRequest
                {
                    AccessToken = auth.Data.AccessToken,
                    RefreshToken = auth.Data.RefreshToken,
                    SessionId = sessionId,
                    Metadata = new RequestMetadata()
                });
        });
        var logoutTask = Task.Run(async () =>
        {
            await start.Task;
            await using var scope = IntegrationTestFixture.Services.CreateAsyncScope();
            IntegrationTestFixture.EstablishTrustedActorContext(
                scope.ServiceProvider,
                seeded.TenantId,
                seeded.CredentialId);
            return await scope.ServiceProvider.GetRequiredService<IAuthService>()
                .LogoutAsync(new LogoutRequest
                {
                    SessionId = sessionId,
                    CredentialId = seeded.CredentialId,
                    Metadata = new RequestMetadata()
                });
        });

        start.SetResult();
        await Task.WhenAll(refreshTask, logoutTask);

        logoutTask.Result.IsSuccess.Should().BeTrue(logoutTask.Result.Message);
        await using var db = CreateDbContext();
        var persisted = await db.Set<Session>()
            .IgnoreQueryFilters()
            .SingleAsync(session => session.Id == sessionId);
        persisted.Status.Should().Be(CurrentSessionState.Inactive);
    }

    [Test]
    public async Task Logout_ActorPlusServiceCannotTerminateAnotherCredentialsSession()
    {
        var seeded = await SeedAuthenticationGraph();
        var auth = await AuthenticateDirect(CreateAuthRequest(
            seeded.TenantId,
            seeded.RoleTypeId,
            seeded.Username,
            seeded.Password));
        auth.IsSuccess.Should().BeTrue(auth.Message);
        var sessionId = auth.Data!.SessionId!.Value;

        await using var scope = IntegrationTestFixture.Services.CreateAsyncScope();
        var actor = new TrustedActorIdentity(
            Guid.NewGuid(),
            Guid.NewGuid(),
            seeded.TenantId,
            Guid.NewGuid(),
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            "identityserver-integration-tests-g1",
            DateTimeOffset.UtcNow.AddHours(1));
        var service = new TrustedServiceIdentity(
            "TestClient",
            XFrameworkServiceNames.IdentityServer,
            new HashSet<string>(XFrameworkServiceScopes.AdminDefaults, StringComparer.Ordinal),
            "test-client-g1");
        scope.ServiceProvider.GetRequiredService<ITrustedInvocationContextStore>().Set(
            new TrustedInvocationContext(actor, service, seeded.TenantId, null, Guid.NewGuid()));

        var result = await scope.ServiceProvider.GetRequiredService<IAuthService>()
            .LogoutAsync(new LogoutRequest
            {
                SessionId = sessionId,
                CredentialId = seeded.CredentialId,
                Metadata = new RequestMetadata()
            });

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
        await using var db = CreateDbContext();
        var persisted = await db.Set<Session>()
            .IgnoreQueryFilters()
            .SingleAsync(session => session.Id == sessionId);
        persisted.Status.Should().Be(CurrentSessionState.Active);
    }

    [Test]
    public async Task Bolt_ValidateIdentitySession_AfterRoleTypeDisabled_ReturnsUnauthorized()
    {
        var seeded = await SeedAuthenticationGraph();
        QueryResponse<AuthenticateIdentityResponse> auth;
        using (IntegrationTestFixture.SuppressActorAccessToken())
        {
            auth = await IntegrationTestFixture.ServiceWrapper.AuthenticateIdentity(
                CreateAuthRequest(seeded.TenantId, seeded.RoleTypeId, seeded.Username, seeded.Password));
        }
        auth.HttpStatusCode.Should().Be(HttpStatusCode.OK, auth.Message);

        await using (var db = CreateDbContext())
        {
            var roleType = await db.Set<IdentityRoleType>()
                .IgnoreQueryFilters()
                .SingleAsync(item => item.Id == seeded.RoleTypeId);
            roleType.IsEnabled = false;
            roleType.ConcurrencyStamp = Guid.NewGuid();
            await db.SaveChangesAsync();
        }

        using var actorAccessToken = IntegrationTestFixture.UseActorAccessToken(auth.Response!.AccessToken!);
        var result = await IntegrationTestFixture.ServiceWrapper.ValidateIdentitySession(
            new ValidateIdentitySessionRequest
            {
                Metadata = new RequestMetadata
                {
                    RequestId = Guid.NewGuid(),
                    OperationName = nameof(Bolt_ValidateIdentitySession_AfterRoleTypeDisabled_ReturnsUnauthorized)
                }
            });

        result.HttpStatusCode.Should().Be(HttpStatusCode.Unauthorized);
        result.IsSuccess.Should().BeFalse();
    }

    [Test]
    public void JsonSerialization_IdentityCredential_DoesNotExposeSecrets()
    {
        var credential = new IdentityCredential
        {
            Id = Guid.NewGuid(),
            PasswordByte = Encoding.UTF8.GetBytes("credential-password-hash"),
            Password = "credential-plaintext-password",
            Token = "credential-bearer-token"
        };

        AssertJsonOmits(
            credential,
            ["PasswordByte", "Password", "Token"],
            ["credential-password-hash", "credential-plaintext-password", "credential-bearer-token"]);
    }

    [Test]
    public void JsonSerialization_Session_DoesNotExposeTokenPayloadOrHash()
    {
        var session = new Session
        {
            Id = Guid.NewGuid(),
            CredentialId = Guid.NewGuid(),
            SessionData = "session-access-and-refresh-token-payload",
            RefreshTokenHash = "session-refresh-token-hash"
        };

        AssertJsonOmits(
            session,
            ["SessionData", "RefreshTokenHash"],
            ["session-access-and-refresh-token-payload", "session-refresh-token-hash"]);
    }

    [Test]
    public void JsonSerialization_IdentityVerification_DoesNotExposeTokenOrHash()
    {
        var verification = new IdentityVerification
        {
            Id = Guid.NewGuid(),
            CredentialId = Guid.NewGuid(),
            Token = "verification-plaintext-token",
            TokenHash = "verification-token-hash"
        };

        AssertJsonOmits(
            verification,
            ["Token", "TokenHash"],
            ["verification-plaintext-token", "verification-token-hash"]);
    }

    [Test]
    public void MemoryPackSerialization_SecretBearingEntities_DoNotExposeSecrets()
    {
        var credential = new IdentityCredential
        {
            Id = Guid.NewGuid(),
            UserName = "memorypack-user",
            PasswordByte = Encoding.UTF8.GetBytes("credential-password-hash"),
            Password = "credential-plaintext-password",
            Token = "credential-bearer-token"
        };
        var session = new Session
        {
            Id = Guid.NewGuid(),
            CredentialId = credential.Id,
            SessionData = "session-access-and-refresh-token-payload",
            RefreshTokenHash = "session-refresh-token-hash"
        };
        var verification = new IdentityVerification
        {
            Id = Guid.NewGuid(),
            CredentialId = credential.Id,
            Token = "verification-plaintext-token",
            TokenHash = "verification-token-hash"
        };

        var credentialCopy = MemoryPackSerializer.Deserialize<IdentityCredential>(
            MemoryPackSerializer.Serialize(credential));
        var sessionCopy = MemoryPackSerializer.Deserialize<Session>(
            MemoryPackSerializer.Serialize(session));
        var verificationCopy = MemoryPackSerializer.Deserialize<IdentityVerification>(
            MemoryPackSerializer.Serialize(verification));

        credentialCopy.Should().NotBeNull();
        credentialCopy!.UserName.Should().Be(credential.UserName);
        credentialCopy.PasswordByte.Should().BeNull();
        credentialCopy.Password.Should().BeNull();
        credentialCopy.Token.Should().BeNull();

        sessionCopy.Should().NotBeNull();
        sessionCopy!.CredentialId.Should().Be(session.CredentialId);
        sessionCopy.SessionData.Should().BeNull();
        sessionCopy.RefreshTokenHash.Should().BeNull();

        verificationCopy.Should().NotBeNull();
        verificationCopy!.CredentialId.Should().Be(verification.CredentialId);
        verificationCopy.Token.Should().BeNull();
        verificationCopy.TokenHash.Should().BeNull();
    }

    private async Task<AuthenticationSeed> SeedAuthenticationGraph(
        bool tenantIsEnabled = true,
        bool tenantIsDeleted = false,
        bool credentialIsEnabled = true,
        bool credentialIsDeleted = false,
        bool identityIsEnabled = true,
        bool identityIsDeleted = false,
        bool roleTypeIsEnabled = true)
    {
        var tenantId = Guid.NewGuid();
        var identityId = Guid.NewGuid();
        var credentialId = Guid.NewGuid();
        var roleTypeGroupId = Guid.NewGuid();
        var roleTypeId = Guid.NewGuid();
        var username = UniqueUsername();
        var password = "ValidPassword123!";
        var now = DateTime.UtcNow;

        await using var db = CreateDbContext();
        db.Set<Tenant>().Add(new Tenant
        {
            Id = tenantId,
            TenantId = tenantId,
            Name = $"Authentication security {tenantId:N}",
            Version = 1,
            IsEnabled = tenantIsEnabled,
            IsDeleted = tenantIsDeleted,
            DeletedAt = tenantIsDeleted ? now : null,
            CreatedAt = now,
            ConcurrencyStamp = Guid.NewGuid()
        });
        db.Set<IdentityInformation>().Add(new IdentityInformation
        {
            Id = identityId,
            TenantId = tenantId,
            FirstName = "Security",
            LastName = "Boundary",
            IsEnabled = identityIsEnabled,
            IsDeleted = identityIsDeleted,
            DeletedAt = identityIsDeleted ? now : null,
            CreatedAt = now,
            ConcurrencyStamp = Guid.NewGuid()
        });
        db.Set<IdentityRoleTypeGroup>().Add(new IdentityRoleTypeGroup
        {
            Id = roleTypeGroupId,
            TenantId = tenantId,
            Name = "Authentication security roles",
            Description = "Authentication lifecycle test roles",
            IsEnabled = true,
            CreatedAt = now,
            ConcurrencyStamp = Guid.NewGuid()
        });
        db.Set<IdentityRoleType>().Add(new IdentityRoleType
        {
            Id = roleTypeId,
            TenantId = tenantId,
            GroupId = roleTypeGroupId,
            Name = "Authentication security role",
            IsEnabled = roleTypeIsEnabled,
            CreatedAt = now,
            ConcurrencyStamp = Guid.NewGuid()
        });
        db.Set<IdentityCredential>().Add(new IdentityCredential
        {
            Id = credentialId,
            TenantId = tenantId,
            IdentityInfoId = identityId,
            UserName = username,
            PasswordByte = Encoding.ASCII.GetBytes(
                BCrypt.Net.BCrypt.HashPassword(password, workFactor: 11)),
            IsEnabled = credentialIsEnabled,
            IsDeleted = credentialIsDeleted,
            DeletedAt = credentialIsDeleted ? now : null,
            CreatedAt = now,
            ConcurrencyStamp = Guid.NewGuid()
        });
        db.Set<IdentityRole>().Add(new IdentityRole
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CredentialId = credentialId,
            TypeId = roleTypeId,
            RoleExpiration = now.AddYears(1),
            IsEnabled = true,
            CreatedAt = now,
            ConcurrencyStamp = Guid.NewGuid()
        });
        db.Set<SessionType>().Add(new SessionType
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "User",
            SystemReferenceId = IdentityConstants.SessionType.User,
            IsEnabled = true,
            CreatedAt = now,
            ConcurrencyStamp = Guid.NewGuid()
        });
        db.Set<TenantModuleFeature>().Add(new TenantModuleFeature
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ModuleKey = TenantModuleFeatureKeys.Identity,
            SubFeatureKey = string.Empty,
            DisplayName = "Identity",
            IsEnabled = true,
            CreatedAt = now,
            ConcurrencyStamp = Guid.NewGuid()
        });
        await db.SaveChangesAsync();

        return new AuthenticationSeed(tenantId, credentialId, roleTypeId, username, password);
    }

    private static AuthenticateIdentityRequest CreateAuthRequest(
        Guid tenantId,
        Guid roleTypeId,
        string username,
        string password,
        AuthorizationType authorizationType = AuthorizationType.Username) => new()
    {
        UserName = username,
        Password = password,
        RoleId = roleTypeId,
        AuthorizationType = authorizationType,
        GenerateToken = true,
        Metadata = new RequestMetadata
        {
            RequestedTenantId = tenantId,
            RequestId = Guid.NewGuid(),
            IpAddress = "127.0.0.1",
            OperationName = "AuthenticationSecurityTests",
            DeviceName = "IntegrationTest",
            UserAgent = "IntegrationTest"
        }
    };

    private static async Task<Result<AuthenticateIdentityResponse>> AuthenticateDirect(
        AuthenticateIdentityRequest request,
        IPAddress? remoteIpAddress = null)
    {
        await using var scope = IntegrationTestFixture.Services.CreateAsyncScope();
        var accessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        accessor.HttpContext = new DefaultHttpContext();
        accessor.HttpContext.Connection.RemoteIpAddress = remoteIpAddress;
        IntegrationTestFixture.EstablishTrustedServiceTargetContext(
            scope.ServiceProvider,
            request.Metadata.RequestedTenantId!.Value);
        return await scope.ServiceProvider.GetRequiredService<IAuthService>()
            .AuthenticateAsync(request);
    }

    private async Task AssertNoSessionExists(Guid credentialId)
    {
        await using var db = CreateDbContext();
        var sessionExists = await db.Set<Session>()
            .IgnoreQueryFilters()
            .AnyAsync(session => session.CredentialId == credentialId);

        sessionExists.Should().BeFalse();
    }

    private async Task<string> SeedEmailContact(
        AuthenticationSeed seeded,
        bool isEnabled,
        bool isDeleted)
    {
        var now = DateTime.UtcNow;
        var email = $"security-{Guid.NewGuid():N}@example.test";
        await using var db = CreateDbContext();
        var group = new IdentityContactGroup
        {
            Id = Guid.NewGuid(),
            TenantId = seeded.TenantId,
            Name = "Authentication contacts",
            IsEnabled = true,
            CreatedAt = now,
            ConcurrencyStamp = Guid.NewGuid()
        };
        var type = new IdentityContactType
        {
            Id = Guid.NewGuid(),
            TenantId = seeded.TenantId,
            Name = nameof(GenericContactType.Email),
            IsEnabled = true,
            CreatedAt = now,
            ConcurrencyStamp = Guid.NewGuid()
        };
        db.Set<IdentityContactGroup>().Add(group);
        db.Set<IdentityContactType>().Add(type);
        db.Set<IdentityContact>().Add(new IdentityContact
        {
            Id = Guid.NewGuid(),
            TenantId = seeded.TenantId,
            CredentialId = seeded.CredentialId,
            GroupId = group.Id,
            TypeId = type.Id,
            Value = email,
            IsEnabled = isEnabled,
            IsDeleted = isDeleted,
            DeletedAt = isDeleted ? now : null,
            CreatedAt = now,
            ConcurrencyStamp = Guid.NewGuid()
        });
        await db.SaveChangesAsync();
        return email;
    }

    private static void AssertJsonOmits<T>(
        T value,
        IReadOnlyCollection<string> forbiddenProperties,
        IReadOnlyCollection<string> forbiddenValues)
    {
        var json = JsonSerializer.Serialize(value);
        using var document = JsonDocument.Parse(json);
        var serializedProperties = document.RootElement
            .EnumerateObject()
            .Select(property => property.Name)
            .ToArray();

        serializedProperties.Should().NotContain(
            property => forbiddenProperties.Contains(property, StringComparer.OrdinalIgnoreCase));
        foreach (var forbiddenValue in forbiddenValues)
        {
            json.Should().NotContain(forbiddenValue);
        }
    }

    private sealed record AuthenticationSeed(
        Guid TenantId,
        Guid CredentialId,
        Guid RoleTypeId,
        string Username,
        string Password);
}
