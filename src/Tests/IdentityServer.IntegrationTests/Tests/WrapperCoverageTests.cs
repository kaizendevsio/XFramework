using System.Net;
using System.Text;
using IdentityServer.Domain.Shared;
using IdentityServer.Domain.Shared.Contracts;
using IdentityServer.Domain.Shared.Contracts.Requests;
using IdentityServer.Domain.Shared.Contracts.Responses;
using Microsoft.EntityFrameworkCore;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts;
using XFramework.Domain.Shared.Enums;
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

        result.HttpStatusCode.Should().Be(HttpStatusCode.OK);
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
        var tenantId = await lookupDb.Set<Tenant>()
            .IgnoreQueryFilters()
            .Where(t => t.Name == tenantName)
            .Select(t => t.Id)
            .FirstAsync();

        var result = await IntegrationTestFixture.ServiceWrapper.DeleteTenant(new DeleteTenantRequest
        {
            TenantId = tenantId,
            Metadata = CreateMetadata()
        });

        result.HttpStatusCode.Should().Be(HttpStatusCode.OK);
        result.IsSuccess.Should().BeTrue();

        await using var db = CreateDbContext();
        var tenant = await db.Set<Tenant>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == tenantId);

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
    public async Task Logout_WithActiveSession_DeactivatesSession()
    {
        var auth = await AuthenticateThroughWrapper();
        var sessionId = auth.Response!.SessionId!.Value;
        var credentialId = auth.Response.Credential!.Id;

        var result = await IntegrationTestFixture.ServiceWrapper.Logout(new LogoutRequest
        {
            SessionId = sessionId,
            CredentialId = credentialId,
            Metadata = CreateMetadata()
        });

        result.HttpStatusCode.Should().Be(HttpStatusCode.OK);
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
            CredentialId = Guid.NewGuid(),
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

        result.HttpStatusCode.Should().Be(HttpStatusCode.OK);
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
    public async Task ResetPassword_WithValidToken_ChangesPasswordAndApprovesVerification()
    {
        const string oldPassword = "OldPassword123!";
        const string newPassword = "NewPassword456!";
        var credential = await SeedCredentialWithRole(UniqueUsername(), oldPassword);
        var token = await SeedPendingPasswordResetVerification(credential.Id);

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
            .FirstOrDefaultAsync(v => v.Token == token);

        verification.Should().NotBeNull();
        verification!.Status.Should().Be((short)GenericStatusType.Approved);
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
        result.Response.AvatarUrl.Should().Contain("identity-credential-avatars/credentials/");
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
            FileBytes = [1, 2, 3],
            Metadata = CreateMetadata()
        });

        result.HttpStatusCode.Should().Be(HttpStatusCode.NotFound);
        result.IsSuccess.Should().BeFalse();
    }

    [Test]
    public async Task UploadCredentialAvatar_WithTenantMismatch_ReturnsNotFound()
    {
        var credential = await SeedCredentialWithRole(UniqueUsername(), "AvatarPassword123!");

        var result = await IntegrationTestFixture.ServiceWrapper.UploadCredentialAvatar(new UploadCredentialAvatarRequest
        {
            CredentialId = credential.Id,
            FileName = "profile.png",
            ContentType = "image/png",
            FileBytes = [1, 2, 3],
            Metadata = CreateMetadata(Guid.NewGuid())
        });

        result.HttpStatusCode.Should().Be(HttpStatusCode.NotFound);
        result.IsSuccess.Should().BeFalse();
    }

    private async Task<QueryResponse<AuthenticateIdentityResponse>> AuthenticateThroughWrapper()
    {
        var username = UniqueUsername();
        var password = "ValidPassword123!";
        await SeedCredentialWithRole(username, password);

        var result = await IntegrationTestFixture.ServiceWrapper.AuthenticateIdentity(new AuthenticateIdentityRequest
        {
            UserName = username,
            Password = password,
            RoleId = TestData.RoleTypeId,
            AuthorizationType = AuthorizationType.Default,
            GenerateToken = true,
            Metadata = CreateMetadata()
        });

        result.HttpStatusCode.Should().Be(HttpStatusCode.OK);
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
            TenantId = IntegrationTestFixture.TestTenantId
        };
        db.Set<IdentityInformation>().Add(info);

        await db.SaveChangesAsync();
        return info;
    }

    private async Task<IdentityCredential> SeedCredentialWithRole(string username, string password)
    {
        await using var db = CreateDbContext();

        var info = new IdentityInformation
        {
            Id = Guid.NewGuid(),
            FirstName = "Test",
            LastName = "User",
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

        var role = new IdentityRole
        {
            Id = Guid.NewGuid(),
            CredentialId = credential.Id,
            TypeId = TestData.RoleTypeId,
            TenantId = IntegrationTestFixture.TestTenantId
        };
        db.Set<IdentityRole>().Add(role);

        await db.SaveChangesAsync();
        return credential;
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
            Token = token,
            Status = (short)GenericStatusType.Pending,
            StatusUpdatedOn = DateTimeOffset.UtcNow,
            Expiry = DateTime.UtcNow.AddMinutes(10),
            TenantId = IntegrationTestFixture.TestTenantId
        });

        await db.SaveChangesAsync();
        return token;
    }

    private async Task<StorageFile> SeedStorageFile(Guid credentialId, string contentType, string fileName)
    {
        await using var db = CreateDbContext();

        var type = new StorageFileType
        {
            Id = Guid.NewGuid(),
            TenantId = IntegrationTestFixture.TestTenantId,
            Name = contentType,
            SystemReferenceId = Guid.NewGuid(),
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow
        };
        db.Set<StorageFileType>().Add(type);

        var group = new StorageFileIdentifierGroup
        {
            Id = Guid.NewGuid(),
            TenantId = IntegrationTestFixture.TestTenantId,
            Name = CredentialAvatarPolicy.StorageIdentifierGroupName,
            SystemReferenceId = Guid.NewGuid(),
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow
        };
        db.Set<StorageFileIdentifierGroup>().Add(group);

        var identifier = new StorageFileIdentifier
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

        var storageFile = new StorageFile
        {
            Id = Guid.NewGuid(),
            TenantId = IntegrationTestFixture.TestTenantId,
            ContentPath = $"https://files.example.test/avatars/{Guid.NewGuid():N}/{fileName}",
            TypeId = type.Id,
            Identifier = credentialId,
            StorageFileIdentifierId = identifier.Id,
            Name = fileName,
            ContentType = contentType,
            BlobContainer = CredentialAvatarPolicy.BlobContainer,
            FileSize = 1,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow
        };
        db.Set<StorageFile>().Add(storageFile);

        await db.SaveChangesAsync();
        return storageFile;
    }

    private static RequestMetadata CreateMetadata(Guid? tenantId = null) => new()
    {
        TenantId = tenantId ?? IntegrationTestFixture.TestTenantId,
        RequestId = Guid.NewGuid(),
        IpAddress = "127.0.0.1",
        Name = "IntegrationTest",
        DeviceName = "TestDevice",
        DeviceAgent = "TestAgent"
    };
}
