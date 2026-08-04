using System.Net;
using System.Security.Claims;
using System.Text;
using System.Security.Cryptography;
using IdentityServer.Domain.Shared;
using IdentityServer.Api.Services;
using IdentityServer.Domain.Shared.Contracts;
using IdentityServer.Domain.Shared.Contracts.Requests;
using IdentityServer.Domain.Shared.Contracts.Responses;
using Microsoft.EntityFrameworkCore;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts;
using XFramework.Domain.Shared.Enums;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Abstractions;
using XFramework.Integration.Security;
using XFramework.TestInfrastructure;
using Session = IdentityServer.Domain.Shared.Contracts.Session;

namespace IdentityServer.IntegrationTests.Tests;

[TestFixture]
[Category(TestCategories.Integration)]
[Category(TestCategories.ExtendedIntegration)]
[Category(TestCategories.IdentityServer)]
[Category(TestCategories.Wrappers)]
public sealed class WrapperCoverageTests : IntegrationTestBase
{
    [SetUp]
    public void ResetWorkflowFailureInjection() => IdentityServerWorkflowFailureInjection.Reset();

    [Test]
    public async Task ServiceSigningKeys_ThroughAuthorizedWrapper_SupportQueryRotationAndRetirement()
    {
        using var actorSuppression = IntegrationTestFixture.SuppressActorAccessToken();
        var initial = await IntegrationTestFixture.ServiceWrapper.GetServiceSigningKeys(
            new GetServiceSigningKeysRequest());

        initial.HttpStatusCode.Should().Be(HttpStatusCode.OK, initial.Message);
        var previousActive = initial.Response!.Keys.Single(key => key.IsActive);

        var rotation = await IntegrationTestFixture.ServiceWrapper.RotateServiceSigningKey(
            new RotateServiceSigningKeyRequest
            {
                Reason = "wrapper-integration-test"
            });

        rotation.HttpStatusCode.Should().Be(HttpStatusCode.OK, rotation.Message);
        rotation.Response.Should().NotBeNull();
        rotation.Response!.IsActive.Should().BeTrue();
        rotation.Response.KeyId.Should().NotBe(previousActive.KeyId);

        var activeRetirement = await IntegrationTestFixture.ServiceWrapper.RetireServiceSigningKey(
            new RetireServiceSigningKeyRequest
            {
                KeyId = rotation.Response.KeyId
            });

        activeRetirement.HttpStatusCode.Should().Be(HttpStatusCode.BadRequest);

        var retirement = await IntegrationTestFixture.ServiceWrapper.RetireServiceSigningKey(
            new RetireServiceSigningKeyRequest
            {
                KeyId = previousActive.KeyId
            });

        retirement.HttpStatusCode.Should().Be(HttpStatusCode.OK, retirement.Message);
        retirement.Response.Should().NotBeNull();
        retirement.Response!.IsActive.Should().BeFalse();
        retirement.Response.RetiredAtUtc.Should().NotBeNull();
    }

    [Test]
    public async Task ServiceSigningKeys_ThroughLimitedScopeWrapper_ReturnsForbidden()
    {
        var wrapper = await IntegrationTestFixture.CreateLimitedScopeServiceWrapper();

        var result = await wrapper.GetServiceSigningKeys(new GetServiceSigningKeysRequest());

        result.HttpStatusCode.Should().Be(HttpStatusCode.Forbidden);
        result.IsSuccess.Should().BeFalse();
    }

    [Test]
    public async Task RotateServiceSigningKey_LimitedActorCannotInheritServiceAdminScope()
    {
        var username = UniqueUsername();
        const string password = "LimitedSigningKeyActor123!";
        await SeedCredentialWithRole(username, password);
        var authentication = await AuthenticateExistingCredential(username, password);

        using var actorAccessToken = IntegrationTestFixture.UseActorAccessToken(
            authentication.Response!.AccessToken!);
        var result = await IntegrationTestFixture.ServiceWrapper.RotateServiceSigningKey(
            new RotateServiceSigningKeyRequest
            {
                Reason = "limited-actor-must-not-inherit-service-admin"
            });

        result.HttpStatusCode.Should().Be(HttpStatusCode.Forbidden);
        result.IsSuccess.Should().BeFalse();
    }

    [Test]
    public async Task TenantAdministration_LimitedActorCannotInheritServiceAdminScopeAtServiceBoundary()
    {
        using var scope = IntegrationTestFixture.Services.CreateScope();
        var actor = new TrustedActorIdentity(
            Guid.NewGuid(),
            Guid.NewGuid(),
            IntegrationTestFixture.TestTenantId,
            Guid.NewGuid(),
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            "limited-actor-g1",
            DateTimeOffset.UtcNow.AddMinutes(5));
        var service = new TrustedServiceIdentity(
            XFrameworkServiceNames.Portal,
            XFrameworkServiceNames.IdentityServer,
            new HashSet<string>([XFrameworkServiceScopes.IdentityAdmin], StringComparer.OrdinalIgnoreCase),
            "portal-service-g1");
        scope.ServiceProvider.GetRequiredService<ITrustedInvocationContextStore>().Set(
            new TrustedInvocationContext(
                actor,
                service,
                IntegrationTestFixture.TestTenantId,
                null,
                Guid.NewGuid()));
        var authorizationService = scope.ServiceProvider.GetRequiredService<IIdentityAuthorizationService>();

        var result = await authorizationService.SetTenantModuleFeaturesAsync(
            new SetTenantModuleFeaturesRequest
            {
                TenantId = IntegrationTestFixture.TestTenantId,
                ExpectedConcurrencyStamp = Guid.NewGuid(),
                Features = [],
                Metadata = CreateMetadata()
            });

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be((int)HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task CreateTenant_WithValidData_CreatesTenantWithServerGeneratedIdentity()
    {
        var tenantName = $"Wrapper Tenant {Guid.NewGuid():N}";

        var result = await IntegrationTestFixture.ServiceWrapper.CreateTenant(new CreateTenantRequest
        {
            Name = tenantName,
            Description = "Created through direct wrapper coverage",
            Version = 1.25m,
            Status = 1,
            ParentTenantId = IntegrationTestFixture.TestTenantId,
            Metadata = CreateMetadata()
        });

        result.HttpStatusCode.Should().Be(HttpStatusCode.OK, result.Message);
        result.IsSuccess.Should().BeTrue();

        await using var db = CreateDbContext();
        var tenant = await db.Set<Tenant>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Name == tenantName);

        tenant.Should().NotBeNull();
        tenant!.Id.Should().NotBeEmpty();
        tenant.TenantId.Should().Be(tenant.Id);
        tenant.ParentTenantId.Should().Be(IntegrationTestFixture.TestTenantId);
        tenant.Version.Should().Be(1.25m);
        tenant.Status.Should().Be(1);
        tenant.IsEnabled.Should().BeTrue();
    }

    [Test]
    public async Task CreateTenant_WithUnknownParent_ReturnsNotFound()
    {
        var result = await IntegrationTestFixture.ServiceWrapper.CreateTenant(new CreateTenantRequest
        {
            Name = $"Invalid Parent Tenant {Guid.NewGuid():N}",
            Version = 1.0m,
            ParentTenantId = Guid.NewGuid(),
            Metadata = CreateMetadata()
        });

        result.HttpStatusCode.Should().Be(HttpStatusCode.NotFound);
        result.IsSuccess.Should().BeFalse();
    }

    [Test]
    public async Task DeleteTenant_WithExistingTenant_SoftDeletesAndDisablesTenant()
    {
        var tenantName = $"Wrapper Delete Tenant {Guid.NewGuid():N}";
        var create = await IntegrationTestFixture.ServiceWrapper.CreateTenant(new CreateTenantRequest
        {
            Name = tenantName,
            Description = "Created for direct delete wrapper coverage",
            Version = 1.0m,
            Status = 1,
            ParentTenantId = IntegrationTestFixture.TestTenantId,
            Metadata = CreateMetadata()
        });

        create.HttpStatusCode.Should().Be(HttpStatusCode.OK);

        await using var lookupDb = CreateDbContext();
        var tenantReference = await lookupDb.Set<Tenant>()
            .IgnoreQueryFilters()
            .Where(t => t.Name == tenantName)
            .Select(t => new { t.Id, t.ConcurrencyStamp })
            .FirstAsync();

        var result = await IntegrationTestFixture.ServiceWrapper.DeleteTenant(new DeleteTenantRequest
        {
            TenantId = tenantReference.Id,
            ExpectedConcurrencyStamp = tenantReference.ConcurrencyStamp,
            Metadata = CreateMetadata()
        });

        result.HttpStatusCode.Should().Be(HttpStatusCode.OK, result.Message);
        result.IsSuccess.Should().BeTrue();

        await using var db = CreateDbContext();
        var tenant = await db.Set<Tenant>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == tenantReference.Id);

        tenant.Should().NotBeNull();
        tenant!.IsDeleted.Should().BeTrue();
        tenant.IsEnabled.Should().BeFalse();
        tenant.DeletedAt.Should().NotBeNull();
    }

    [Test]
    public async Task DeleteTenant_WithUnknownTenant_ReturnsNotFound()
    {
        var result = await IntegrationTestFixture.ServiceWrapper.DeleteTenant(new DeleteTenantRequest
        {
            TenantId = Guid.NewGuid(),
            ExpectedConcurrencyStamp = Guid.NewGuid(),
            Metadata = CreateMetadata()
        });

        result.HttpStatusCode.Should().Be(HttpStatusCode.NotFound);
        result.IsSuccess.Should().BeFalse();
    }

    [Test]
    public async Task CreateCredential_WithValidData_HashesPasswordAndCreatesCredential()
    {
        var info = await SeedIdentityInfo();
        var username = UniqueUsername();
        var password = "WrapperPassword123!";

        var result = await IntegrationTestFixture.ServiceWrapper.CreateCredential(new CreateCredentialRequest
        {
            IdentityInfoId = info.Id,
            UserName = username,
            UserAlias = "Wrapper Alias",
            Password = password,
            Metadata = CreateMetadata()
        });

        result.HttpStatusCode.Should().Be(HttpStatusCode.OK);
        result.IsSuccess.Should().BeTrue();

        await using var db = CreateDbContext();
        var credential = await db.Set<IdentityCredential>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.IdentityInfoId == info.Id && c.UserName == username);

        credential.Should().NotBeNull();
        credential!.PasswordByte.Should().NotBeNullOrEmpty();
        BCrypt.Net.BCrypt.Verify(password, Encoding.ASCII.GetString(credential.PasswordByte!)).Should().BeTrue();

        var verify = await IntegrationTestFixture.ServiceWrapper.VerifyPassword(
            new VerifyPasswordRequest
            {
                CredentialId = credential.Id,
                Password = password,
                Metadata = CreateMetadata()
            });

        verify.HttpStatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test]
    public async Task CreateCredential_WithUnknownIdentity_ReturnsNotFound()
    {
        var result = await IntegrationTestFixture.ServiceWrapper.CreateCredential(new CreateCredentialRequest
        {
            IdentityInfoId = Guid.NewGuid(),
            UserName = UniqueUsername(),
            Password = "WrapperPassword123!",
            Metadata = CreateMetadata()
        });

        result.HttpStatusCode.Should().Be(HttpStatusCode.NotFound);
        result.IsSuccess.Should().BeFalse();
    }

    [Test]
    public async Task DeleteTenant_WithoutExpectedConcurrencyStamp_ReturnsBadRequest()
    {
        var result = await IntegrationTestFixture.ServiceWrapper.DeleteTenant(new DeleteTenantRequest
        {
            TenantId = Guid.NewGuid(),
            Metadata = CreateMetadata()
        });

        result.HttpStatusCode.Should().Be(HttpStatusCode.BadRequest);
        result.IsSuccess.Should().BeFalse();
    }

    [Test]
    public async Task ForgotPassword_WithUnknownEmail_ReturnsSuccess()
    {
        var result = await IntegrationTestFixture.ServiceWrapper.ForgotPassword(new ForgotPasswordRequest
        {
            Email = UniqueEmail(),
            Metadata = CreateMetadata()
        });

        result.HttpStatusCode.Should().Be(HttpStatusCode.OK);
        result.IsSuccess.Should().BeTrue();
    }

    [Test]
    public async Task ForgotPassword_WithoutContact_ReturnsBadRequest()
    {
        var result = await IntegrationTestFixture.ServiceWrapper.ForgotPassword(new ForgotPasswordRequest
        {
            Metadata = CreateMetadata()
        });

        result.HttpStatusCode.Should().Be(HttpStatusCode.BadRequest);
        result.IsSuccess.Should().BeFalse();
    }

    [Test]
    public async Task Logout_WithActiveSession_DeactivatesSession()
    {
        var auth = await AuthenticateThroughWrapper();
        var sessionId = auth.Response!.SessionId!.Value;
        var credentialId = auth.Response.Credential!.Id;

        using var actorAccessToken = IntegrationTestFixture.UseActorAccessToken(auth.Response.AccessToken!);
        var result = await IntegrationTestFixture.ServiceWrapper.Logout(new LogoutRequest
        {
            SessionId = sessionId,
            CredentialId = credentialId,
            Metadata = CreateMetadata()
        });

        result.HttpStatusCode.Should().Be(HttpStatusCode.OK, result.Message);
        result.IsSuccess.Should().BeTrue();

        await using var db = CreateDbContext();
        var session = await db.Set<Session>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == sessionId);

        session.Should().NotBeNull();
        session!.Status.Should().Be(CurrentSessionState.Inactive);
    }

    [Test]
    public async Task Logout_WithUnknownSession_ReturnsNotFound()
    {
        var result = await IntegrationTestFixture.ServiceWrapper.Logout(new LogoutRequest
        {
            SessionId = Guid.NewGuid(),
            CredentialId = IntegrationTestFixture.TestCredentialId,
            Metadata = CreateMetadata()
        });

        result.HttpStatusCode.Should().Be(HttpStatusCode.NotFound);
        result.IsSuccess.Should().BeFalse();
    }

    [Test]
    public async Task RefreshToken_WithValidTokens_ReturnsNewTokenPair()
    {
        var auth = await AuthenticateThroughWrapper();
        var authResponse = auth.Response!;

        var result = await IntegrationTestFixture.ServiceWrapper.RefreshToken(new RefreshTokenRequest
        {
            AccessToken = authResponse.AccessToken,
            RefreshToken = authResponse.RefreshToken,
            SessionId = authResponse.SessionId!.Value,
            Metadata = CreateMetadata()
        });

        result.HttpStatusCode.Should().Be(HttpStatusCode.OK, result.Message);
        result.Response.Should().NotBeNull();
        result.Response!.AccessToken.Should().NotBeNullOrEmpty();
        result.Response.RefreshToken.Should().NotBeNullOrEmpty();
        result.Response.SessionId.Should().Be(authResponse.SessionId.Value);
        result.Response.ExpiresIn.Should().BeGreaterThan(0);
    }

    [Test]
    public async Task RefreshToken_WithInvalidRefreshToken_ReturnsUnauthorized()
    {
        var auth = await AuthenticateThroughWrapper();
        var authResponse = auth.Response!;

        var result = await IntegrationTestFixture.ServiceWrapper.RefreshToken(new RefreshTokenRequest
        {
            AccessToken = authResponse.AccessToken,
            RefreshToken = "invalid_refresh_token",
            SessionId = authResponse.SessionId!.Value,
            Metadata = CreateMetadata()
        });

        result.HttpStatusCode.Should().Be(HttpStatusCode.Unauthorized);
        result.IsSuccess.Should().BeFalse();
    }

    [Test]
    public async Task ValidateIdentitySession_WithActiveLifecycle_ReturnsValid()
    {
        var auth = await AuthenticateThroughWrapper();

        using var actorAccessToken = IntegrationTestFixture.UseActorAccessToken(auth.Response!.AccessToken!);
        var result = await IntegrationTestFixture.ServiceWrapper.ValidateIdentitySession(
            new ValidateIdentitySessionRequest
            {
                Metadata = CreateMetadata()
            });

        result.HttpStatusCode.Should().Be(HttpStatusCode.OK, result.Message);
        result.Response.Should().NotBeNull();
        result.Response!.IsValid.Should().BeTrue();
    }

    [Test]
    public async Task ValidateIdentitySession_AfterLogout_ReturnsUnauthorized()
    {
        var auth = await AuthenticateThroughWrapper();
        var credentialId = auth.Response!.Credential!.Id;
        var sessionId = auth.Response.SessionId!.Value;

        using var actorAccessToken = IntegrationTestFixture.UseActorAccessToken(auth.Response.AccessToken!);
        var logout = await IntegrationTestFixture.ServiceWrapper.Logout(new LogoutRequest
        {
            SessionId = sessionId,
            CredentialId = credentialId,
            Metadata = CreateMetadata()
        });
        logout.HttpStatusCode.Should().Be(HttpStatusCode.OK);

        var result = await IntegrationTestFixture.ServiceWrapper.ValidateIdentitySession(
            new ValidateIdentitySessionRequest
            {
                Metadata = CreateMetadata()
            });

        result.HttpStatusCode.Should().Be(HttpStatusCode.Unauthorized);
        result.IsSuccess.Should().BeFalse();
    }

    [Test]
    public async Task Http_Logout_ForAnotherCredential_IsForbiddenAndKeepsSessionActive()
    {
        var auth = await AuthenticateThroughWrapper();
        var sessionId = auth.Response!.SessionId!.Value;
        var credentialId = auth.Response.Credential!.Id;

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout")
        {
            Content = JsonContent.Create(new LogoutRequest
            {
                SessionId = sessionId,
                CredentialId = credentialId,
                Metadata = CreateMetadata()
            })
        };
        request.Headers.Add(TestAuthHeaders.CredentialId, Guid.NewGuid().ToString("D"));
        request.Headers.Add(TestAuthHeaders.TenantId, IntegrationTestFixture.TestTenantId.ToString("D"));

        using var response = await HttpClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await using var db = CreateDbContext();
        var session = await db.Set<Session>().IgnoreQueryFilters().SingleAsync(x => x.Id == sessionId);
        session.Status.Should().Be(CurrentSessionState.Active);
    }

    [Test]
    public async Task RefreshToken_WithAccessTokenForDifferentTenant_ReturnsUnauthorized()
    {
        var auth = await AuthenticateThroughWrapper();
        var authResponse = auth.Response!;

        using var scope = IntegrationTestFixture.Services.CreateScope();
        var jwtService = scope.ServiceProvider.GetRequiredService<IJwtService>();
        var (principal, _) = await jwtService.DecodeExpiredToken(authResponse.AccessToken!);
        var claims = principal.Claims
            .Where(claim => claim.Type is not "tenant_id" and not "tenantId")
            .ToList();
        var mismatchedTenantId = Guid.NewGuid();
        claims.Add(new Claim("tenant_id", mismatchedTenantId.ToString("D")));
        claims.Add(new Claim("tenantId", mismatchedTenantId.ToString("D")));
        var mismatchedTenantToken = await jwtService.GenerateToken(claims);

        var result = await IntegrationTestFixture.ServiceWrapper.RefreshToken(new RefreshTokenRequest
        {
            AccessToken = mismatchedTenantToken.AccessToken,
            RefreshToken = authResponse.RefreshToken,
            SessionId = authResponse.SessionId!.Value,
            Metadata = CreateMetadata()
        });

        result.HttpStatusCode.Should().Be(HttpStatusCode.Unauthorized);
        result.IsSuccess.Should().BeFalse();
    }

    [Test]
    public async Task RefreshToken_WithAccessTokenFromAnotherSession_ReturnsUnauthorized()
    {
        var username = UniqueUsername();
        const string password = "ValidPassword123!";
        await SeedCredentialWithRole(username, password);
        var first = await AuthenticateExistingCredential(username, password);
        var second = await AuthenticateExistingCredential(username, password);

        var result = await IntegrationTestFixture.ServiceWrapper.RefreshToken(new RefreshTokenRequest
        {
            AccessToken = second.Response!.AccessToken,
            RefreshToken = first.Response!.RefreshToken,
            SessionId = first.Response.SessionId!.Value,
            Metadata = CreateMetadata()
        });

        result.HttpStatusCode.Should().Be(HttpStatusCode.Unauthorized);
        result.IsSuccess.Should().BeFalse();
    }

    [TestCase(null)]
    [TestCase("not-a-session-id")]
    public async Task RefreshToken_WithMissingOrMalformedSessionClaim_ReturnsUnauthorized(string? sessionClaim)
    {
        var auth = await AuthenticateThroughWrapper();
        var authResponse = auth.Response!;
        using var scope = IntegrationTestFixture.Services.CreateScope();
        var jwtService = scope.ServiceProvider.GetRequiredService<IJwtService>();
        var (principal, _) = await jwtService.DecodeExpiredToken(authResponse.AccessToken!);
        var claims = principal.Claims
            .Where(claim => claim.Type != "session_id")
            .ToList();
        if (sessionClaim is not null)
            claims.Add(new Claim("session_id", sessionClaim));
        var mismatchedToken = await jwtService.GenerateToken(claims);

        var result = await IntegrationTestFixture.ServiceWrapper.RefreshToken(new RefreshTokenRequest
        {
            AccessToken = mismatchedToken.AccessToken,
            RefreshToken = authResponse.RefreshToken,
            SessionId = authResponse.SessionId!.Value,
            Metadata = CreateMetadata()
        });

        result.HttpStatusCode.Should().Be(HttpStatusCode.Unauthorized);
        result.IsSuccess.Should().BeFalse();
    }

    [Test]
    public async Task ResetPassword_WithValidToken_ConsumesTokenAndRejectsReplay()
    {
        const string oldPassword = "OldPassword123!";
        const string newPassword = "NewPassword456!";
        var credential = await SeedCredentialWithRole(UniqueUsername(), oldPassword);
        var token = await SeedPendingPasswordResetVerification(credential.Id);
        var sessionId = await SeedActiveSession(credential.Id);

        var result = await IntegrationTestFixture.ServiceWrapper.ResetPassword(new ResetPasswordRequest
        {
            Token = token,
            NewPassword = newPassword,
            Metadata = CreateMetadata()
        });

        result.HttpStatusCode.Should().Be(HttpStatusCode.OK);
        result.IsSuccess.Should().BeTrue();

        var verifyOld = await IntegrationTestFixture.ServiceWrapper.VerifyPassword(
            new VerifyPasswordRequest
            {
                CredentialId = credential.Id,
                Password = oldPassword,
                Metadata = CreateMetadata()
            });
        verifyOld.HttpStatusCode.Should().Be(HttpStatusCode.BadRequest);

        var verifyNew = await IntegrationTestFixture.ServiceWrapper.VerifyPassword(
            new VerifyPasswordRequest
            {
                CredentialId = credential.Id,
                Password = newPassword,
                Metadata = CreateMetadata()
            });
        verifyNew.HttpStatusCode.Should().Be(HttpStatusCode.OK);

        await using var db = CreateDbContext();
        var verification = await db.Set<IdentityVerification>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(v => v.TokenHash == HashToken(token));

        verification.Should().NotBeNull();
        verification!.Status.Should().Be((short)GenericStatusType.Approved);
        verification.ConsumedAt.Should().NotBeNull();
        var session = await db.Set<Session>()
            .IgnoreQueryFilters()
            .FirstAsync(item => item.Id == sessionId);
        session.Status.Should().Be(CurrentSessionState.Inactive);

        var replay = await IntegrationTestFixture.ServiceWrapper.ResetPassword(new ResetPasswordRequest
        {
            Token = token,
            NewPassword = "ReplayPassword789!",
            Metadata = CreateMetadata()
        });

        replay.HttpStatusCode.Should().Be(HttpStatusCode.BadRequest);
        replay.IsSuccess.Should().BeFalse();
    }

    [Test]
    public async Task ResetPassword_ConcurrentConsumers_AllowExactlyOnePasswordChange()
    {
        var credential = await SeedCredentialWithRole(UniqueUsername(), "ConcurrentResetOriginal123!");
        var token = await SeedPendingPasswordResetVerification(credential.Id);
        const string firstPassword = "ConcurrentResetFirst123!";
        const string secondPassword = "ConcurrentResetSecond123!";
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<CmdResponse> ResetAsync(string password)
        {
            await start.Task;
            return await IntegrationTestFixture.ServiceWrapper.ResetPassword(new ResetPasswordRequest
            {
                Token = token,
                NewPassword = password,
                Metadata = CreateMetadata()
            });
        }

        var attempts = new[] { ResetAsync(firstPassword), ResetAsync(secondPassword) };
        start.SetResult();
        var results = await Task.WhenAll(attempts);

        results.Count(result => result.IsSuccess).Should().Be(1);
        var verificationResults = await Task.WhenAll(
            IntegrationTestFixture.ServiceWrapper.VerifyPassword(new VerifyPasswordRequest
            {
                CredentialId = credential.Id,
                Password = firstPassword,
                Metadata = CreateMetadata()
            }),
            IntegrationTestFixture.ServiceWrapper.VerifyPassword(new VerifyPasswordRequest
            {
                CredentialId = credential.Id,
                Password = secondPassword,
                Metadata = CreateMetadata()
            }));
        verificationResults.Count(result => result.IsSuccess).Should().Be(1);
    }

    [Test]
    public async Task ResetPassword_WithInvalidToken_ReturnsBadRequest()
    {
        var result = await IntegrationTestFixture.ServiceWrapper.ResetPassword(new ResetPasswordRequest
        {
            Token = "missing_" + Guid.NewGuid().ToString("N"),
            NewPassword = "NewPassword456!",
            Metadata = CreateMetadata()
        });

        result.HttpStatusCode.Should().Be(HttpStatusCode.BadRequest);
        result.IsSuccess.Should().BeFalse();
    }

    [Test]
    public async Task UploadCredentialAvatar_WithValidImage_UploadsStorageFileAndUpdatesCredential()
    {
        var credential = await SeedCredentialWithRole(UniqueUsername(), "AvatarPassword123!");
        var imageBytes = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 };

        var result = await IntegrationTestFixture.ServiceWrapper.UploadCredentialAvatar(new UploadCredentialAvatarRequest
        {
            CredentialId = credential.Id,
            FileName = "profile.png",
            ContentType = "image/png",
            FileBytes = imageBytes,
            Metadata = CreateMetadata()
        });

        result.HttpStatusCode.Should().Be(HttpStatusCode.OK, result.Message);
        result.IsSuccess.Should().BeTrue();
        result.Response.Should().NotBeNull();
        result.Response!.CredentialId.Should().Be(credential.Id);
        result.Response.StorageFileId.Should().NotBeNull();
        result.Response.AvatarUrl.Should().Contain($"/{IntegrationTestFixture.TestTenantId:N}/");
        result.Response.ContentType.Should().Be("image/png");
        result.Response.FileName.Should().Be("profile.png");
        result.Response.AvatarUpdatedAt.Should().NotBeNull();

        await using var db = CreateDbContext();
        var savedCredential = await db.Set<IdentityCredential>()
            .IgnoreQueryFilters()
            .FirstAsync(c => c.Id == credential.Id);
        savedCredential.AvatarStorageFileId.Should().Be(result.Response.StorageFileId);
        savedCredential.AvatarUrl.Should().Be(result.Response.AvatarUrl);

        var storageFile = await db.Set<StorageFile>()
            .IgnoreQueryFilters()
            .FirstAsync(file => file.Id == result.Response.StorageFileId);
        storageFile.ContentType.Should().Be("image/png");
        storageFile.ContentLengthBytes.Should().Be(imageBytes.Length);
        storageFile.Status.Should().Be(StorageFileStatus.Available);
    }

    [Test]
    public async Task SetCredentialAvatar_WithExistingImageStorageFile_AttachesAvatar()
    {
        var credential = await SeedCredentialWithRole(UniqueUsername(), "AvatarPassword123!");
        var storageFile = await SeedStorageFile(credential.Id, "image/jpeg", "existing.jpg");

        var result = await IntegrationTestFixture.ServiceWrapper.SetCredentialAvatar(new SetCredentialAvatarRequest
        {
            CredentialId = credential.Id,
            StorageFileId = storageFile.Id,
            Metadata = CreateMetadata()
        });

        result.HttpStatusCode.Should().Be(HttpStatusCode.OK);
        result.IsSuccess.Should().BeTrue();
        result.Response.Should().NotBeNull();
        result.Response!.StorageFileId.Should().Be(storageFile.Id);
        result.Response.AvatarUrl.Should().Be(storageFile.ContentPath);
        result.Response.ContentType.Should().Be("image/jpeg");
        result.Response.FileName.Should().Be("existing.jpg");
    }

    [Test]
    public async Task SetCredentialAvatar_WithNonImageStorageFile_ReturnsBadRequest()
    {
        var credential = await SeedCredentialWithRole(UniqueUsername(), "AvatarPassword123!");
        var storageFile = await SeedStorageFile(credential.Id, "application/pdf", "document.pdf");

        var result = await IntegrationTestFixture.ServiceWrapper.SetCredentialAvatar(new SetCredentialAvatarRequest
        {
            CredentialId = credential.Id,
            StorageFileId = storageFile.Id,
            Metadata = CreateMetadata()
        });

        result.HttpStatusCode.Should().Be(HttpStatusCode.BadRequest);
        result.IsSuccess.Should().BeFalse();
    }

    [Test]
    public async Task SetCredentialAvatar_WithDifferentCredentialStorageFile_ReturnsForbidden()
    {
        var credential = await SeedCredentialWithRole(UniqueUsername(), "AvatarPassword123!");
        var otherCredential = await SeedCredentialWithRole(UniqueUsername(), "AvatarPassword123!");
        var storageFile = await SeedStorageFile(otherCredential.Id, "image/jpeg", "other.jpg");

        var result = await IntegrationTestFixture.ServiceWrapper.SetCredentialAvatar(new SetCredentialAvatarRequest
        {
            CredentialId = credential.Id,
            StorageFileId = storageFile.Id,
            Metadata = CreateMetadata()
        });

        result.HttpStatusCode.Should().Be(HttpStatusCode.Forbidden);
        result.IsSuccess.Should().BeFalse();
    }

    [Test]
    public async Task RemoveCredentialAvatar_WithExistingAvatar_ClearsAvatarMetadata()
    {
        var credential = await SeedCredentialWithRole(UniqueUsername(), "AvatarPassword123!");
        var storageFile = await SeedStorageFile(credential.Id, "image/webp", "avatar.webp");

        await using (var db = CreateDbContext())
        {
            var tracked = await db.Set<IdentityCredential>()
                .IgnoreQueryFilters()
                .FirstAsync(c => c.Id == credential.Id);
            tracked.AvatarStorageFileId = storageFile.Id;
            tracked.AvatarUrl = storageFile.ContentPath;
            tracked.AvatarUpdatedAt = DateTime.UtcNow.AddMinutes(-5);
            db.Update(tracked);
            await db.SaveChangesAsync();
        }

        var result = await IntegrationTestFixture.ServiceWrapper.RemoveCredentialAvatar(new RemoveCredentialAvatarRequest
        {
            CredentialId = credential.Id,
            Metadata = CreateMetadata()
        });

        result.HttpStatusCode.Should().Be(HttpStatusCode.OK);
        result.IsSuccess.Should().BeTrue();
        result.Response.Should().NotBeNull();
        result.Response!.StorageFileId.Should().BeNull();
        result.Response.AvatarUrl.Should().BeNull();
        result.Response.AvatarUpdatedAt.Should().BeNull();

        await using var verifyDb = CreateDbContext();
        var savedCredential = await verifyDb.Set<IdentityCredential>()
            .IgnoreQueryFilters()
            .FirstAsync(c => c.Id == credential.Id);
        savedCredential.AvatarStorageFileId.Should().BeNull();
        savedCredential.AvatarUrl.Should().BeNull();
        savedCredential.AvatarUpdatedAt.Should().BeNull();

        var savedFile = await verifyDb.Set<StorageFile>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(file => file.Id == storageFile.Id);
        savedFile.Should().NotBeNull("removing an avatar should not delete the stored file");
    }

    [Test]
    public async Task RemoveCredentialAvatar_WithUnknownCredential_ReturnsNotFound()
    {
        var result = await IntegrationTestFixture.ServiceWrapper.RemoveCredentialAvatar(
            new RemoveCredentialAvatarRequest
            {
                CredentialId = Guid.NewGuid(),
                Metadata = CreateMetadata()
            });

        result.HttpStatusCode.Should().Be(HttpStatusCode.NotFound);
        result.IsSuccess.Should().BeFalse();
    }

    [Test]
    public async Task ResetPassword_WithManyPendingTokens_CancelsThemWithBoundedDatabaseCommands()
    {
        const int pendingTokenCount = 128;
        var credential = await SeedCredentialWithRole(UniqueUsername(), "OldPassword123!");
        var acceptedToken = await SeedPendingPasswordResetVerification(credential.Id);
        await using (var seedDb = CreateDbContext())
        {
            seedDb.Set<IdentityVerification>().AddRange(
                Enumerable.Range(0, pendingTokenCount).Select(_ => new IdentityVerification
                {
                    Id = Guid.NewGuid(),
                    CredentialId = credential.Id,
                    VerificationTypeId = IdentityConstants.VerificationType.Email,
                    TokenHash = HashToken("other_" + Guid.NewGuid().ToString("N")),
                    Purpose = IdentityConstants.VerificationPurpose.PasswordReset,
                    Status = (short)GenericStatusType.Pending,
                    StatusUpdatedOn = DateTimeOffset.UtcNow,
                    Expiry = DateTime.UtcNow.AddMinutes(10),
                    TenantId = IntegrationTestFixture.TestTenantId,
                    IsEnabled = true,
                    ConcurrencyStamp = Guid.NewGuid()
                }));
            await seedDb.SaveChangesAsync();
        }

        var commandCounter = IntegrationTestFixture.Services.GetRequiredService<DbCommandCounterInterceptor>();
        using var measurement = commandCounter.BeginMeasurement();
        var result = await IntegrationTestFixture.ServiceWrapper.ResetPassword(new ResetPasswordRequest
        {
            Token = acceptedToken,
            NewPassword = "NewPassword456!",
            Metadata = CreateMetadata()
        });

        result.HttpStatusCode.Should().Be(HttpStatusCode.OK, result.Message);
        measurement.CommandCount.Should().BeLessThan(25,
            "pending reset tokens must be canceled with a fixed-count bulk update");
        await using var assertionDb = CreateDbContext();
        var canceledCount = await assertionDb.Set<IdentityVerification>()
            .IgnoreQueryFilters()
            .CountAsync(item => item.CredentialId == credential.Id
                                && item.Purpose == IdentityConstants.VerificationPurpose.PasswordReset
                                && item.Status == (short)GenericStatusType.Canceled);
        canceledCount.Should().Be(pendingTokenCount);
    }

    [Test]
    public async Task UploadCredentialAvatar_WithInvalidContentType_ReturnsBadRequest()
    {
        var credential = await SeedCredentialWithRole(UniqueUsername(), "AvatarPassword123!");

        var result = await IntegrationTestFixture.ServiceWrapper.UploadCredentialAvatar(new UploadCredentialAvatarRequest
        {
            CredentialId = credential.Id,
            FileName = "profile.txt",
            ContentType = "text/plain",
            FileBytes = [1, 2, 3],
            Metadata = CreateMetadata()
        });

        result.HttpStatusCode.Should().Be(HttpStatusCode.BadRequest);
        result.IsSuccess.Should().BeFalse();
    }

    [Test]
    public async Task UploadCredentialAvatar_WithOversizedImage_ReturnsBadRequest()
    {
        var credential = await SeedCredentialWithRole(UniqueUsername(), "AvatarPassword123!");

        var result = await IntegrationTestFixture.ServiceWrapper.UploadCredentialAvatar(new UploadCredentialAvatarRequest
        {
            CredentialId = credential.Id,
            FileName = "profile.png",
            ContentType = "image/png",
            FileBytes = new byte[CredentialAvatarPolicy.MaxFileSizeBytes + 1],
            Metadata = CreateMetadata()
        });

        result.HttpStatusCode.Should().Be(HttpStatusCode.BadRequest);
        result.IsSuccess.Should().BeFalse();
    }

    [Test]
    public async Task UploadCredentialAvatar_WithUnknownCredential_ReturnsNotFound()
    {
        var result = await IntegrationTestFixture.ServiceWrapper.UploadCredentialAvatar(new UploadCredentialAvatarRequest
        {
            CredentialId = Guid.NewGuid(),
            FileName = "profile.png",
            ContentType = "image/png",
            FileBytes = [137, 80, 78, 71, 13, 10, 26, 10],
            Metadata = CreateMetadata()
        });

        result.HttpStatusCode.Should().Be(HttpStatusCode.NotFound);
        result.IsSuccess.Should().BeFalse();
    }

    [Test]
    public async Task UploadCredentialAvatar_WithUnknownDelegatedTenant_ReturnsForbiddenWithoutDisclosure()
    {
        var credential = await SeedCredentialWithRole(UniqueUsername(), "AvatarPassword123!");

        var result = await IntegrationTestFixture.ServiceWrapper.UploadCredentialAvatar(new UploadCredentialAvatarRequest
        {
            CredentialId = credential.Id,
            FileName = "profile.png",
            ContentType = "image/png",
            FileBytes = [137, 80, 78, 71, 13, 10, 26, 10],
            Metadata = CreateMetadata(Guid.NewGuid())
        });

        result.HttpStatusCode.Should().Be(HttpStatusCode.Forbidden);
        result.IsSuccess.Should().BeFalse();
    }

    [Test]
    public async Task UploadCredentialAvatar_WithImageMimeButInvalidSignature_ReturnsBadRequest()
    {
        var credential = await SeedCredentialWithRole(UniqueUsername(), "AvatarPassword123!");

        var result = await IntegrationTestFixture.ServiceWrapper.UploadCredentialAvatar(new UploadCredentialAvatarRequest
        {
            CredentialId = credential.Id,
            FileName = "profile.png",
            ContentType = "image/png",
            FileBytes = [1, 2, 3, 4, 5, 6, 7, 8],
            Metadata = CreateMetadata()
        });

        result.HttpStatusCode.Should().Be(HttpStatusCode.BadRequest);
        result.IsSuccess.Should().BeFalse();
    }

    [Test]
    public async Task SetTenantModuleFeatures_UpsertsTenantOwnedFeatureConfiguration()
    {
        var subFeature = $"integration_{Guid.NewGuid():N}";
        await using var tenantDb = CreateDbContext();
        var tenantStamp = await tenantDb.Set<Tenant>()
            .IgnoreQueryFilters()
            .Where(tenant => tenant.Id == IntegrationTestFixture.TestTenantId)
            .Select(tenant => tenant.ConcurrencyStamp)
            .SingleAsync();
        var result = await IntegrationTestFixture.ServiceWrapper.SetTenantModuleFeatures(
            new SetTenantModuleFeaturesRequest
            {
                TenantId = IntegrationTestFixture.TestTenantId,
                ExpectedConcurrencyStamp = tenantStamp,
                Metadata = CreateMetadata(),
                Features =
                [
                    new TenantModuleFeatureUpdate
                    {
                        ModuleKey = TenantModuleFeatureKeys.Identity,
                        SubFeatureKey = subFeature,
                        DisplayName = "Integration feature",
                        Description = "Direct wrapper coverage",
                        IsEnabled = true
                    }
                ]
            });

        result.HttpStatusCode.Should().Be(HttpStatusCode.OK, result.Message);
        result.IsSuccess.Should().BeTrue();

        await using var db = CreateDbContext();
        var feature = await db.Set<TenantModuleFeature>()
            .IgnoreQueryFilters()
            .SingleAsync(x => x.TenantId == IntegrationTestFixture.TestTenantId &&
                              x.ModuleKey == TenantModuleFeatureKeys.Identity &&
                              x.SubFeatureKey == subFeature);
        feature.IsEnabled.Should().BeTrue();
    }

    [Test]
    public async Task SetTenantModuleFeatures_WithStaleTenantVersion_ReturnsConflict()
    {
        var result = await IntegrationTestFixture.ServiceWrapper.SetTenantModuleFeatures(
            new SetTenantModuleFeaturesRequest
            {
                TenantId = IntegrationTestFixture.TestTenantId,
                ExpectedConcurrencyStamp = Guid.NewGuid(),
                Metadata = CreateMetadata(),
                Features =
                [
                    new TenantModuleFeatureUpdate
                    {
                        ModuleKey = TenantModuleFeatureKeys.Identity,
                        SubFeatureKey = $"stale_{Guid.NewGuid():N}",
                        IsEnabled = true
                    }
                ]
            });

        result.HttpStatusCode.Should().Be(HttpStatusCode.Conflict);
        result.IsSuccess.Should().BeFalse();
    }

    [Test]
    public async Task TenantAuthorizationPolicy_WrapperCanGetAndUpdateMissingPermissionBehavior()
    {
        var actorSnapshot = await IntegrationTestFixture.ServiceWrapper.ValidateIdentitySession(
            new ValidateIdentitySessionRequest { Metadata = CreateMetadata() });
        actorSnapshot.HttpStatusCode.Should().Be(HttpStatusCode.OK, actorSnapshot.Message);
        actorSnapshot.Response!.Capabilities.Should().Contain(
            "identity.tenants:manage",
            $"the integration administrator must carry tenant delegation authority; actual capabilities: {string.Join(", ", actorSnapshot.Response.Capabilities)}");

        var tenantName = $"Authorization Policy Tenant {Guid.NewGuid():N}";
        var create = await IntegrationTestFixture.ServiceWrapper.CreateTenant(new CreateTenantRequest
        {
            Name = tenantName,
            Description = "Created for authorization policy wrapper coverage",
            Version = 1.0m,
            Status = 1,
            ParentTenantId = IntegrationTestFixture.TestTenantId,
            Metadata = CreateMetadata()
        });
        create.HttpStatusCode.Should().Be(HttpStatusCode.OK);

        await using var db = CreateDbContext();
        var createdTenant = await db.Set<Tenant>()
            .IgnoreQueryFilters()
            .Where(t => t.Name == tenantName)
            .FirstAsync();
        createdTenant.IsEnabled.Should().BeTrue();
        createdTenant.IsDeleted.Should().BeFalse();
        createdTenant.AvailabilityDate.Should().BeNull();
        createdTenant.Expiration.Should().BeNull();
        var tenantId = createdTenant.Id;

        var initializedIdentityFeatures = await db.Set<TenantModuleFeature>()
            .IgnoreQueryFilters()
            .Where(feature => feature.TenantId == tenantId &&
                              feature.ModuleKey == TenantModuleFeatureKeys.Identity &&
                              feature.IsEnabled &&
                              !feature.IsDeleted)
            .CountAsync();
        initializedIdentityFeatures.Should().BeGreaterThan(0);

        var initial = await IntegrationTestFixture.ServiceWrapper.GetTenantAuthorizationPolicy(new GetTenantAuthorizationPolicyRequest
        {
            TenantId = tenantId,
            Metadata = CreateMetadata(tenantId)
        });

        initial.HttpStatusCode.Should().Be(HttpStatusCode.OK, initial.Message);
        initial.Response.Should().NotBeNull();
        initial.Response!.MissingPermissionBehavior.Should().Be(MissingPermissionBehavior.Deny);

        var update = await IntegrationTestFixture.ServiceWrapper.UpdateTenantAuthorizationPolicy(new UpdateTenantAuthorizationPolicyRequest
        {
            TenantId = tenantId,
            MissingPermissionBehavior = MissingPermissionBehavior.Allow,
            ExpectedConcurrencyStamp = initial.Response.ConcurrencyStamp,
            Metadata = CreateMetadata(tenantId)
        });

        update.HttpStatusCode.Should().Be(HttpStatusCode.OK, update.Message);
        update.Response.Should().NotBeNull();
        update.Response!.MissingPermissionBehavior.Should().Be(MissingPermissionBehavior.Allow);
    }

    [Test]
    public async Task RoleTypePermissions_WrapperCanSetGetAndCheckCredentialCapability()
    {
        var roleType = await SeedRoleType("Capability Role");
        var credential = await SeedCredentialWithRole(UniqueUsername(), "CapabilityPassword123!", roleType.Id);

        var set = await IntegrationTestFixture.ServiceWrapper.SetRoleTypePermissions(new SetRoleTypePermissionsRequest
        {
            RoleTypeId = roleType.Id,
            ExpectedConcurrencyStamp = roleType.ConcurrencyStamp,
            Permissions =
            [
                new CapabilityPermissionDto
                {
                    ModuleKey = TenantModuleFeatureKeys.Identity,
                    CapabilityKey = IdentityAuthorizationConstants.View,
                    Effect = RoleCapabilityPermissionEffect.Allow
                }
            ],
            Metadata = CreateMetadata()
        });

        set.HttpStatusCode.Should().Be(HttpStatusCode.OK);
        set.Response.Should().NotBeNull();
        set.Response!.Permissions.Should().ContainSingle(x =>
            x.ModuleKey == TenantModuleFeatureKeys.Identity &&
            x.CapabilityKey == IdentityAuthorizationConstants.View &&
            x.Effect == RoleCapabilityPermissionEffect.Allow);

        var get = await IntegrationTestFixture.ServiceWrapper.GetRoleTypePermissions(new GetRoleTypePermissionsRequest
        {
            RoleTypeId = roleType.Id,
            Metadata = CreateMetadata()
        });

        get.HttpStatusCode.Should().Be(HttpStatusCode.OK);
        get.Response.Should().NotBeNull();
        get.Response!.Permissions.Should().ContainSingle(x =>
            x.ModuleKey == TenantModuleFeatureKeys.Identity &&
            x.CapabilityKey == IdentityAuthorizationConstants.View);

        var stale = await IntegrationTestFixture.ServiceWrapper.SetRoleTypePermissions(new SetRoleTypePermissionsRequest
        {
            RoleTypeId = roleType.Id,
            ExpectedConcurrencyStamp = roleType.ConcurrencyStamp,
            Permissions = [],
            Metadata = CreateMetadata()
        });
        stale.HttpStatusCode.Should().Be(HttpStatusCode.Conflict);

        var allowed = await IntegrationTestFixture.ServiceWrapper.CheckCredentialCapability(new CheckCredentialCapabilityRequest
        {
            CredentialId = credential.Id,
            ModuleKey = TenantModuleFeatureKeys.Identity,
            CapabilityKey = IdentityAuthorizationConstants.View,
            Metadata = CreateMetadata()
        });

        allowed.HttpStatusCode.Should().Be(HttpStatusCode.OK);
        allowed.Response.Should().NotBeNull();
        allowed.Response!.IsAllowed.Should().BeTrue();

        var denied = await IntegrationTestFixture.ServiceWrapper.CheckCredentialCapability(new CheckCredentialCapabilityRequest
        {
            CredentialId = credential.Id,
            ModuleKey = TenantModuleFeatureKeys.Identity,
            CapabilityKey = IdentityAuthorizationConstants.Delete,
            Metadata = CreateMetadata()
        });

        denied.HttpStatusCode.Should().Be(HttpStatusCode.OK);
        denied.Response.Should().NotBeNull();
        denied.Response!.IsAllowed.Should().BeFalse();
    }

    [Test]
    public async Task RoleTypePermissions_ConcurrentWriters_RejectOneStaleVersion()
    {
        var roleType = await SeedRoleType("Concurrent Capability Role");
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<QueryResponse<RoleTypePermissionsResponse>> SetAsync(string capability)
        {
            await start.Task;
            return await IntegrationTestFixture.ServiceWrapper.SetRoleTypePermissions(
                new SetRoleTypePermissionsRequest
                {
                    RoleTypeId = roleType.Id,
                    ExpectedConcurrencyStamp = roleType.ConcurrencyStamp,
                    Permissions =
                    [
                        new CapabilityPermissionDto
                        {
                            ModuleKey = TenantModuleFeatureKeys.Identity,
                            CapabilityKey = capability,
                            Effect = RoleCapabilityPermissionEffect.Allow
                        }
                    ],
                    Metadata = CreateMetadata()
                });
        }

        var attempts = new[]
        {
            SetAsync(IdentityAuthorizationConstants.View),
            SetAsync(IdentityAuthorizationConstants.Update)
        };
        start.SetResult();
        var results = await Task.WhenAll(attempts);

        results.Count(result => result.IsSuccess).Should().Be(1);
        results.Count(result => result.HttpStatusCode == HttpStatusCode.Conflict).Should().Be(1);
    }

    [Test]
    public async Task CredentialRolePermissionOverrides_WrapperCanSetGetAndOverrideRoleTypePermissions()
    {
        var roleType = await SeedRoleType("Override Role");
        var credential = await SeedCredentialWithoutRole(UniqueUsername(), "OverridePassword123!");

        var assign = await IntegrationTestFixture.ServiceWrapper.AssignCredentialRole(new AssignCredentialRoleRequest
        {
            CredentialId = credential.Id,
            RoleTypeId = roleType.Id,
            RoleExpiration = DateTime.UtcNow.AddYears(1),
            Metadata = CreateMetadata()
        });
        assign.HttpStatusCode.Should().Be(HttpStatusCode.OK);
        assign.Response.Should().NotBeNull();

        await IntegrationTestFixture.ServiceWrapper.SetRoleTypePermissions(new SetRoleTypePermissionsRequest
        {
            RoleTypeId = roleType.Id,
            ExpectedConcurrencyStamp = roleType.ConcurrencyStamp,
            Permissions =
            [
                new CapabilityPermissionDto
                {
                    ModuleKey = TenantModuleFeatureKeys.Identity,
                    CapabilityKey = IdentityAuthorizationConstants.View,
                    Effect = RoleCapabilityPermissionEffect.Allow
                }
            ],
            Metadata = CreateMetadata()
        });

        var setOverrides = await IntegrationTestFixture.ServiceWrapper.SetCredentialRolePermissionOverrides(
            new SetCredentialRolePermissionOverridesRequest
            {
                IdentityRoleId = assign.Response!.Id,
                ExpectedConcurrencyStamp = assign.Response.ConcurrencyStamp,
                Overrides =
                [
                    new CapabilityPermissionDto
                    {
                        ModuleKey = TenantModuleFeatureKeys.Identity,
                        CapabilityKey = IdentityAuthorizationConstants.View,
                        Effect = RoleCapabilityPermissionEffect.Deny
                    },
                    new CapabilityPermissionDto
                    {
                        ModuleKey = TenantModuleFeatureKeys.Identity,
                        CapabilityKey = IdentityAuthorizationConstants.Delete,
                        Effect = RoleCapabilityPermissionEffect.Allow
                    }
                ],
                Metadata = CreateMetadata()
            });

        setOverrides.HttpStatusCode.Should().Be(HttpStatusCode.OK);
        setOverrides.Response.Should().NotBeNull();
        setOverrides.Response!.Overrides.Should().HaveCount(2);

        var getOverrides = await IntegrationTestFixture.ServiceWrapper.GetCredentialRolePermissionOverrides(
            new GetCredentialRolePermissionOverridesRequest
            {
                IdentityRoleId = assign.Response.Id,
                Metadata = CreateMetadata()
            });

        getOverrides.HttpStatusCode.Should().Be(HttpStatusCode.OK);
        getOverrides.Response.Should().NotBeNull();
        getOverrides.Response!.Overrides.Should().Contain(x =>
            x.CapabilityKey == IdentityAuthorizationConstants.View &&
            x.Effect == RoleCapabilityPermissionEffect.Deny);

        var deniedView = await IntegrationTestFixture.ServiceWrapper.CheckCredentialCapability(new CheckCredentialCapabilityRequest
        {
            CredentialId = credential.Id,
            ModuleKey = TenantModuleFeatureKeys.Identity,
            CapabilityKey = IdentityAuthorizationConstants.View,
            Metadata = CreateMetadata()
        });
        deniedView.Response!.IsAllowed.Should().BeFalse();

        var allowedDelete = await IntegrationTestFixture.ServiceWrapper.CheckCredentialCapability(new CheckCredentialCapabilityRequest
        {
            CredentialId = credential.Id,
            ModuleKey = TenantModuleFeatureKeys.Identity,
            CapabilityKey = IdentityAuthorizationConstants.Delete,
            Metadata = CreateMetadata()
        });
        allowedDelete.Response!.IsAllowed.Should().BeTrue();
    }

    [Test]
    public async Task AssignAndRemoveCredentialRole_WrapperMutatesRoleThroughAuthorizationApi()
    {
        var roleType = await SeedRoleType("Assignable Role");
        var credential = await SeedCredentialWithoutRole(UniqueUsername(), "AssignPassword123!");

        var assign = await IntegrationTestFixture.ServiceWrapper.AssignCredentialRole(new AssignCredentialRoleRequest
        {
            CredentialId = credential.Id,
            RoleTypeId = roleType.Id,
            RoleExpiration = DateTime.UtcNow.AddMonths(6),
            Metadata = CreateMetadata()
        });

        assign.HttpStatusCode.Should().Be(HttpStatusCode.OK);
        assign.Response.Should().NotBeNull();
        assign.Response!.CredentialId.Should().Be(credential.Id);
        assign.Response.RoleTypeId.Should().Be(roleType.Id);

        var remove = await IntegrationTestFixture.ServiceWrapper.RemoveCredentialRole(new RemoveCredentialRoleRequest
        {
            IdentityRoleId = assign.Response.Id,
            Metadata = CreateMetadata()
        });

        remove.HttpStatusCode.Should().Be(HttpStatusCode.OK);
        remove.IsSuccess.Should().BeTrue();

        await using var db = CreateDbContext();
        var savedRole = await db.Set<IdentityRole>()
            .IgnoreQueryFilters()
            .FirstAsync(x => x.Id == assign.Response.Id);

        savedRole.IsDeleted.Should().BeTrue();
        savedRole.IsEnabled.Should().BeFalse();
    }

    [Test]
    public async Task GetEffectiveCredentialCapabilities_WrapperReturnsResolvedCapabilities()
    {
        var credential = await SeedCredentialWithRole(UniqueUsername(), "EffectivePassword123!");

        var result = await IntegrationTestFixture.ServiceWrapper.GetEffectiveCredentialCapabilities(
            new GetEffectiveCredentialCapabilitiesRequest
            {
                CredentialId = credential.Id,
                Metadata = CreateMetadata()
            });

        result.HttpStatusCode.Should().Be(HttpStatusCode.OK, result.Message);
        result.Response.Should().NotBeNull();
        result.Response!.Capabilities.Should().Contain(x =>
            x.ModuleKey == TenantModuleFeatureKeys.Identity &&
            x.CapabilityKey == IdentityAuthorizationConstants.View &&
            x.IsAllowed);
    }

    [Test]
    public async Task AuthorizationWrappers_DoNotDiscloseUnknownDelegatedTenants()
    {
        var unknownTenantId = Guid.NewGuid();
        var unknownCredentialId = Guid.NewGuid();
        var unknownRoleTypeId = Guid.NewGuid();
        var unknownIdentityRoleId = Guid.NewGuid();

        var getPolicy = await IntegrationTestFixture.ServiceWrapper.GetTenantAuthorizationPolicy(
            new GetTenantAuthorizationPolicyRequest
            {
                TenantId = unknownTenantId,
                Metadata = CreateMetadata(unknownTenantId)
            });
        getPolicy.HttpStatusCode.Should().Be(HttpStatusCode.Forbidden);

        var updatePolicy = await IntegrationTestFixture.ServiceWrapper.UpdateTenantAuthorizationPolicy(
            new UpdateTenantAuthorizationPolicyRequest
            {
                TenantId = unknownTenantId,
                MissingPermissionBehavior = MissingPermissionBehavior.Deny,
                Metadata = CreateMetadata(unknownTenantId)
            });
        updatePolicy.HttpStatusCode.Should().Be(HttpStatusCode.Forbidden);

        var getRolePermissions = await IntegrationTestFixture.ServiceWrapper.GetRoleTypePermissions(
            new GetRoleTypePermissionsRequest
            {
                RoleTypeId = unknownRoleTypeId,
                Metadata = CreateMetadata()
            });
        getRolePermissions.HttpStatusCode.Should().Be(HttpStatusCode.NotFound);

        var getOverrides = await IntegrationTestFixture.ServiceWrapper.GetCredentialRolePermissionOverrides(
            new GetCredentialRolePermissionOverridesRequest
            {
                IdentityRoleId = unknownIdentityRoleId,
                Metadata = CreateMetadata()
            });
        getOverrides.HttpStatusCode.Should().Be(HttpStatusCode.NotFound);

        var setOverrides = await IntegrationTestFixture.ServiceWrapper.SetCredentialRolePermissionOverrides(
            new SetCredentialRolePermissionOverridesRequest
            {
                IdentityRoleId = unknownIdentityRoleId,
                ExpectedConcurrencyStamp = Guid.NewGuid(),
                Metadata = CreateMetadata()
            });
        setOverrides.HttpStatusCode.Should().Be(HttpStatusCode.NotFound);

        var assignRole = await IntegrationTestFixture.ServiceWrapper.AssignCredentialRole(
            new AssignCredentialRoleRequest
            {
                CredentialId = unknownCredentialId,
                RoleTypeId = unknownRoleTypeId,
                RoleExpiration = DateTime.UtcNow.AddMonths(1),
                Metadata = CreateMetadata()
            });
        assignRole.HttpStatusCode.Should().Be(HttpStatusCode.NotFound);

        var removeRole = await IntegrationTestFixture.ServiceWrapper.RemoveCredentialRole(
            new RemoveCredentialRoleRequest
            {
                IdentityRoleId = unknownIdentityRoleId,
                Metadata = CreateMetadata()
            });
        removeRole.HttpStatusCode.Should().Be(HttpStatusCode.NotFound);

        var effective = await IntegrationTestFixture.ServiceWrapper.GetEffectiveCredentialCapabilities(
            new GetEffectiveCredentialCapabilitiesRequest
            {
                CredentialId = unknownCredentialId,
                Metadata = CreateMetadata()
            });
        effective.HttpStatusCode.Should().Be(HttpStatusCode.NotFound);

        var check = await IntegrationTestFixture.ServiceWrapper.CheckCredentialCapability(
            new CheckCredentialCapabilityRequest
            {
                CredentialId = unknownCredentialId,
                ModuleKey = TenantModuleFeatureKeys.Identity,
                CapabilityKey = IdentityAuthorizationConstants.View,
                Metadata = CreateMetadata()
            });
        check.HttpStatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<QueryResponse<AuthenticateIdentityResponse>> AuthenticateThroughWrapper()
    {
        var username = UniqueUsername();
        var password = "ValidPassword123!";
        await SeedCredentialWithRole(username, password);

        return await AuthenticateExistingCredential(username, password);
    }

    private static async Task<QueryResponse<AuthenticateIdentityResponse>> AuthenticateExistingCredential(
        string username,
        string password)
    {

        QueryResponse<AuthenticateIdentityResponse> result;
        using (IntegrationTestFixture.SuppressActorAccessToken())
        {
            result = await IntegrationTestFixture.ServiceWrapper.AuthenticateIdentity(new AuthenticateIdentityRequest
            {
                UserName = username,
                Password = password,
                RoleId = TestData.RoleTypeId,
                AuthorizationType = AuthorizationType.Default,
                GenerateToken = true,
                Metadata = CreateMetadata(IntegrationTestFixture.TestTenantId)
            });
        }

        result.HttpStatusCode.Should().Be(HttpStatusCode.OK, result.Message);
        result.Response.Should().NotBeNull();
        result.Response!.AccessToken.Should().NotBeNullOrEmpty();
        result.Response.RefreshToken.Should().NotBeNullOrEmpty();
        result.Response.SessionId.Should().NotBeNull();
        result.Response.Credential.Should().NotBeNull();

        return result;
    }

    private async Task<IdentityInformation> SeedIdentityInfo()
    {
        await using var db = CreateDbContext();

        var info = new IdentityInformation
        {
            Id = Guid.NewGuid(),
            FirstName = "Test",
            LastName = "User",
            IsEnabled = true,
            TenantId = IntegrationTestFixture.TestTenantId
        };
        db.Set<IdentityInformation>().Add(info);

        await db.SaveChangesAsync();
        return info;
    }

    private async Task<IdentityCredential> SeedCredentialWithRole(string username, string password, Guid? roleTypeId = null)
    {
        var credential = await SeedCredentialWithoutRole(username, password);

        await using var db = CreateDbContext();
        var role = new IdentityRole
        {
            Id = Guid.NewGuid(),
            CredentialId = credential.Id,
            TypeId = roleTypeId ?? TestData.RoleTypeId,
            RoleExpiration = DateTime.UtcNow.AddYears(1),
            IsEnabled = true,
            TenantId = IntegrationTestFixture.TestTenantId
        };
        db.Set<IdentityRole>().Add(role);

        await db.SaveChangesAsync();
        return credential;
    }

    private async Task<IdentityCredential> SeedCredentialWithoutRole(string username, string password)
    {
        await using var db = CreateDbContext();

        var info = new IdentityInformation
        {
            Id = Guid.NewGuid(),
            FirstName = "Test",
            LastName = "User",
            IsEnabled = true,
            TenantId = IntegrationTestFixture.TestTenantId
        };
        db.Set<IdentityInformation>().Add(info);

        var credential = new IdentityCredential
        {
            Id = Guid.NewGuid(),
            UserName = username,
            PasswordByte = Encoding.ASCII.GetBytes(
                BCrypt.Net.BCrypt.HashPassword(password, workFactor: 11)),
            IdentityInfoId = info.Id,
            IsEnabled = true,
            TenantId = IntegrationTestFixture.TestTenantId
        };
        db.Set<IdentityCredential>().Add(credential);

        await db.SaveChangesAsync();
        return credential;
    }

    private async Task<IdentityRoleType> SeedRoleType(string namePrefix)
    {
        await using var db = CreateDbContext();
        var roleType = new IdentityRoleType
        {
            Id = Guid.NewGuid(),
            TenantId = IntegrationTestFixture.TestTenantId,
            Name = $"{namePrefix} {Guid.NewGuid():N}",
            GroupId = XFramework.TestInfrastructure.TestConstants.RoleGroupId,
            RoleLevel = 10,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            ConcurrencyStamp = Guid.NewGuid()
        };
        db.Set<IdentityRoleType>().Add(roleType);
        await db.SaveChangesAsync();
        return roleType;
    }

    private async Task<string> SeedPendingPasswordResetVerification(Guid credentialId)
    {
        var token = "reset_" + Guid.NewGuid().ToString("N");

        await using var db = CreateDbContext();
        db.Set<IdentityVerification>().Add(new IdentityVerification
        {
            Id = Guid.NewGuid(),
            CredentialId = credentialId,
            VerificationTypeId = IdentityConstants.VerificationType.Email,
            TokenHash = HashToken(token),
            Purpose = IdentityConstants.VerificationPurpose.PasswordReset,
            Status = (short)GenericStatusType.Pending,
            StatusUpdatedOn = DateTimeOffset.UtcNow,
            Expiry = DateTime.UtcNow.AddMinutes(10),
            TenantId = IntegrationTestFixture.TestTenantId,
            IsEnabled = true,
            ConcurrencyStamp = Guid.NewGuid()
        });

        await db.SaveChangesAsync();
        return token;
    }

    private async Task<Guid> SeedActiveSession(Guid credentialId)
    {
        await using var db = CreateDbContext();
        var sessionTypeId = await db.Set<SessionType>()
            .IgnoreQueryFilters()
            .Where(type => type.TenantId == IntegrationTestFixture.TestTenantId)
            .Where(type => type.SystemReferenceId == IdentityConstants.SessionType.User)
            .Select(type => type.Id)
            .FirstAsync();
        var session = new Session
        {
            Id = Guid.NewGuid(),
            TenantId = IntegrationTestFixture.TestTenantId,
            CredentialId = credentialId,
            SessionTypeId = sessionTypeId,
            Status = CurrentSessionState.Active,
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            ConcurrencyStamp = Guid.NewGuid()
        };
        db.Set<Session>().Add(session);
        await db.SaveChangesAsync();
        return session.Id;
    }

    private async Task<(IdentityCredential Credential, string Email)> SeedCredentialWithEmailContact()
    {
        var credential = await SeedCredentialWithRole(UniqueUsername(), "ForgotPassword123!");
        var email = UniqueEmail();

        await using var db = CreateDbContext();
        var group = await db.Set<IdentityContactGroup>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(item => item.TenantId == IntegrationTestFixture.TestTenantId);
        if (group is null)
        {
            group = new IdentityContactGroup
            {
                Id = Guid.NewGuid(),
                TenantId = IntegrationTestFixture.TestTenantId,
                Name = "Wrapper contacts",
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                ConcurrencyStamp = Guid.NewGuid()
            };
            db.Add(group);
        }

        var type = await db.Set<IdentityContactType>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(item =>
                item.TenantId == IntegrationTestFixture.TestTenantId &&
                item.Name == nameof(GenericContactType.Email));
        if (type is null)
        {
            type = new IdentityContactType
            {
                Id = Guid.NewGuid(),
                TenantId = IntegrationTestFixture.TestTenantId,
                Name = nameof(GenericContactType.Email),
                SystemReferenceId = Guid.NewGuid(),
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                ConcurrencyStamp = Guid.NewGuid()
            };
            db.Add(type);
        }

        db.Add(new IdentityContact
        {
            Id = Guid.NewGuid(),
            TenantId = IntegrationTestFixture.TestTenantId,
            CredentialId = credential.Id,
            GroupId = group.Id,
            TypeId = type.Id,
            Value = email,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            ConcurrencyStamp = Guid.NewGuid()
        });
        await db.SaveChangesAsync();
        return (credential, email);
    }

    private static UploadCredentialAvatarRequest CreateAvatarUploadRequest(Guid credentialId) => new()
    {
        CredentialId = credentialId,
        FileName = "profile.png",
        ContentType = "image/png",
        FileBytes = [137, 80, 78, 71, 13, 10, 26, 10],
        Metadata = CreateMetadata()
    };

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    private async Task<StorageFile> SeedStorageFile(Guid credentialId, string contentType, string fileName)
    {
        await using var db = CreateDbContext();
        var metadataSuffix = Guid.NewGuid().ToString("N");

        var type = new StorageFileType
        {
            Id = Guid.NewGuid(),
            TenantId = IntegrationTestFixture.TestTenantId,
            Name = $"{contentType}-{metadataSuffix}",
            SystemReferenceId = Guid.NewGuid(),
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow
        };
        db.Set<StorageFileType>().Add(type);

        var group = await db.Set<StorageFileIdentifierGroup>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(item =>
                item.TenantId == IntegrationTestFixture.TestTenantId &&
                item.Name == CredentialAvatarPolicy.StorageIdentifierGroupName);
        if (group is null)
        {
            group = new StorageFileIdentifierGroup
            {
                Id = Guid.NewGuid(),
                TenantId = IntegrationTestFixture.TestTenantId,
                Name = CredentialAvatarPolicy.StorageIdentifierGroupName,
                SystemReferenceId = Guid.NewGuid(),
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow
            };
            db.Set<StorageFileIdentifierGroup>().Add(group);
        }

        var identifier = await db.Set<StorageFileIdentifier>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(item =>
                item.TenantId == IntegrationTestFixture.TestTenantId &&
                item.GroupId == group.Id &&
                item.Name == CredentialAvatarPolicy.StorageFileIdentifierName);
        if (identifier is null)
        {
            identifier = new StorageFileIdentifier
            {
                Id = Guid.NewGuid(),
                TenantId = IntegrationTestFixture.TestTenantId,
                Name = CredentialAvatarPolicy.StorageFileIdentifierName,
                Description = "Identity credential avatar image",
                GroupId = group.Id,
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow
            };
            db.Set<StorageFileIdentifier>().Add(identifier);
        }

        var storageFileId = Guid.NewGuid();
        var bucketName = $"xframework-test-{IntegrationTestFixture.TestTenantId:N}";
        var objectKey = $"{IntegrationTestFixture.TestTenantId:N}/{storageFileId:N}/{fileName}";
        var contentPath = $"https://files.example.test/{bucketName}/{objectKey}";
        var storageFile = new StorageFile
        {
            Id = storageFileId,
            TenantId = IntegrationTestFixture.TestTenantId,
            ContentPath = contentPath,
            ObjectKey = objectKey,
            PublicUrl = contentPath,
            TypeId = type.Id,
            Identifier = credentialId,
            StorageFileIdentifierId = identifier.Id,
            Name = fileName,
            ContentType = contentType,
            BlobContainer = bucketName,
            BucketName = bucketName,
            FileSize = 1,
            ContentLengthBytes = 1,
            Status = StorageFileStatus.Available,
            Visibility = StorageFileVisibility.Public,
            CompletedAt = DateTime.UtcNow,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow
        };
        db.Set<StorageFile>().Add(storageFile);

        await db.SaveChangesAsync();
        return storageFile;
    }

    private static RequestMetadata CreateMetadata(Guid? tenantId = null) => new()
    {
        RequestedTenantId = tenantId,
        RequestId = Guid.NewGuid(),
        IpAddress = "127.0.0.1",
        OperationName = "IntegrationTest",
        DeviceName = "TestDevice",
        UserAgent = "TestAgent"
    };
}
