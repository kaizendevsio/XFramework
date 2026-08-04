using System.Net;
using System.Text;
using Communications.Domain.Shared;
using IdentityServer.Domain.Shared;
using IdentityServer.Domain.Shared.Contracts;
using IdentityServer.Domain.Shared.Contracts.Requests;
using Microsoft.EntityFrameworkCore;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts;
using XFramework.Domain.Shared.Enums;
using XFramework.TestInfrastructure;

namespace IdentityServer.IntegrationTests.Tests;

[TestFixture]
[NonParallelizable]
[Category(TestCategories.Integration)]
[Category(TestCategories.ExtendedIntegration)]
[Category(TestCategories.IdentityServer)]
public sealed class WorkflowCompensationTests : IntegrationTestBase
{
    [SetUp]
    public void ResetFailureInjection() => IdentityServerWorkflowFailureInjection.Reset();

    [TearDown]
    public void ClearFailureInjection() => IdentityServerWorkflowFailureInjection.Reset();

    [TestCase(TestStorageFailurePoint.UploadPart)]
    [TestCase(TestStorageFailurePoint.CompleteUpload)]
    public async Task UploadCredentialAvatar_WhenStorageUploadFails_AbortsUploadSession(
        TestStorageFailurePoint failurePoint)
    {
        var credential = await SeedCredentialAsync();
        IdentityServerWorkflowFailureInjection.StorageFailurePoint = failurePoint;

        var result = await IntegrationTestFixture.ServiceWrapper.UploadCredentialAvatar(
            CreateAvatarRequest(credential.Id));

        result.IsSuccess.Should().BeFalse();
        result.HttpStatusCode.Should().Be(HttpStatusCode.BadGateway);
        (await WaitForAbortCountAsync()).Should().Be(1);

        await using var db = CreateDbContext();
        var file = await db.Set<StorageFile>()
            .IgnoreQueryFilters()
            .SingleAsync(item => item.Identifier == credential.Id);
        file.Status.Should().Be(StorageFileStatus.Failed);
    }

    [Test]
    public async Task UploadCredentialAvatar_WhenCredentialSaveConflicts_DeletesCompletedStorageFile()
    {
        var credential = await SeedCredentialAsync();
        IdentityServerWorkflowFailureInjection.StorageFailurePoint =
            TestStorageFailurePoint.CredentialAvatarPersistence;

        var result = await IntegrationTestFixture.ServiceWrapper.UploadCredentialAvatar(
            CreateAvatarRequest(credential.Id));

        result.IsSuccess.Should().BeFalse();
        result.HttpStatusCode.Should().Be(HttpStatusCode.Conflict);
        IdentityServerWorkflowFailureInjection.DeletedFileCount.Should().Be(1);

        await using var db = CreateDbContext();
        var file = await db.Set<StorageFile>()
            .IgnoreQueryFilters()
            .SingleAsync(item => item.Identifier == credential.Id);
        file.IsDeleted.Should().BeTrue();
        file.Status.Should().Be(StorageFileStatus.Deleted);

        var persistedCredential = await db.Set<IdentityCredential>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(item => item.Id == credential.Id);
        persistedCredential.AvatarStorageFileId.Should().BeNull();
        (await db.Set<StorageClaimOutboxMessage>()
                .IgnoreQueryFilters()
                .CountAsync(message => message.StorageFileId == file.Id))
            .Should().Be(0, "the credential attachment and claim outbox must commit atomically");
    }

    [Test]
    public async Task UploadCredentialAvatar_WhenImmediateClaimFails_DurableOutboxRetriesIdempotently()
    {
        var credential = await SeedCredentialAsync();
        var request = CreateAvatarRequest(credential.Id);
        IdentityServerWorkflowFailureInjection.RejectNextStorageClaims();

        var result = await IntegrationTestFixture.ServiceWrapper.UploadCredentialAvatar(request);

        result.IsSuccess.Should().BeTrue(result.Message);
        var requestId = request.Metadata.RequestId!.Value;
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            await using var db = CreateDbContext();
            var outbox = await db.Set<StorageClaimOutboxMessage>()
                .IgnoreQueryFilters()
                .AsNoTracking()
                .SingleOrDefaultAsync(message =>
                    message.TenantId == IntegrationTestFixture.TestTenantId &&
                    message.RequestId == requestId);
            var file = await db.Set<StorageFile>()
                .IgnoreQueryFilters()
                .AsNoTracking()
                .SingleAsync(item => item.Identifier == credential.Id);
            if (outbox?.ProcessedAt is not null && file.UnclaimedUntil is null)
            {
                outbox.DeadLetteredAt.Should().BeNull();
                IdentityServerWorkflowFailureInjection.StorageClaimAttemptCount.Should().BeGreaterThanOrEqualTo(2);
                return;
            }

            await Task.Delay(100);
        }

        Assert.Fail("Durable avatar storage claim did not complete within the expected interval.");
    }

    [Test]
    public async Task UploadCredentialAvatar_WhenImmediateDeleteFails_DurablyRetriesCleanup()
    {
        var credential = await SeedCredentialAsync();
        IdentityServerWorkflowFailureInjection.StorageFailurePoint =
            TestStorageFailurePoint.CredentialAvatarPersistence;
        IdentityServerWorkflowFailureInjection.FailNextStorageDelete = true;

        var result = await IntegrationTestFixture.ServiceWrapper.UploadCredentialAvatar(
            CreateAvatarRequest(credential.Id));

        result.IsSuccess.Should().BeFalse();
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            await using var db = CreateDbContext();
            var file = await db.Set<StorageFile>()
                .IgnoreQueryFilters()
                .AsNoTracking()
                .SingleAsync(item => item.Identifier == credential.Id);
            var cleanup = await db.Set<StorageCleanupOutboxMessage>()
                .IgnoreQueryFilters()
                .AsNoTracking()
                .SingleOrDefaultAsync(message => message.StorageFileId == file.Id);
            if (cleanup?.ProcessedAt is not null && file.IsDeleted)
            {
                IdentityServerWorkflowFailureInjection.DeletedFileCount.Should().BeGreaterThanOrEqualTo(2);
                return;
            }

            await Task.Delay(100);
        }

        throw new AssertionException("Durable avatar cleanup did not complete within 10 seconds.");
    }

    [Test]
    public async Task StorageCleanupDispatcher_DoesNotProcessDisabledOrDeletedMessages()
    {
        var disabled = new StorageCleanupOutboxMessage
        {
            Id = Guid.NewGuid(),
            TenantId = IntegrationTestFixture.TestTenantId,
            StorageFileId = Guid.NewGuid(),
            RequestId = Guid.NewGuid(),
            IsEnabled = false,
            IsDeleted = false
        };
        var deleted = new StorageCleanupOutboxMessage
        {
            Id = Guid.NewGuid(),
            TenantId = IntegrationTestFixture.TestTenantId,
            StorageFileId = Guid.NewGuid(),
            RequestId = Guid.NewGuid(),
            IsEnabled = true,
            IsDeleted = true,
            DeletedAt = DateTime.UtcNow
        };

        await using (var db = CreateDbContext())
        {
            db.AddRange(disabled, deleted);
            await db.SaveChangesAsync();
        }

        await Task.Delay(TimeSpan.FromSeconds(3));

        await using var verifyDb = CreateDbContext();
        var messages = await verifyDb.Set<StorageCleanupOutboxMessage>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(message => message.Id == disabled.Id || message.Id == deleted.Id)
            .ToListAsync();
        messages.Should().HaveCount(2);
        messages.Should().OnlyContain(message =>
            message.ProcessedAt == null && message.Attempts == 0 && message.LeaseOwner == null);
        IdentityServerWorkflowFailureInjection.DeletedFileCount.Should().Be(0);
    }

    [Test]
    public async Task UploadCredentialAvatar_WhenRequestIsCanceled_AbortsOnceAndPropagatesCancellation()
    {
        var credential = await SeedCredentialAsync();
        IdentityServerWorkflowFailureInjection.StorageFailurePoint = TestStorageFailurePoint.CancelUploadPart;
        using var cancellation = new CancellationTokenSource();

        var upload = IntegrationTestFixture.ServiceWrapper.UploadCredentialAvatar(
            CreateAvatarRequest(credential.Id),
            cancellation.Token);
        await IdentityServerWorkflowFailureInjection.CancelUploadPartReached.WaitAsync(
            TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        var action = async () => await upload;

        await action.Should().ThrowAsync<OperationCanceledException>();
        (await WaitForAbortCountAsync()).Should().Be(1);
    }

    [Test]
    public async Task ForgotPassword_WhenDeliveryFails_InvalidatesPendingResetToken()
    {
        var (credential, email) = await SeedCredentialWithEmailAsync();
        IdentityServerWorkflowFailureInjection.FailCommunicationsDelivery = true;
        var metadata = CreateMetadata(targetTenant: true);

        var result = await IntegrationTestFixture.ServiceWrapper.ForgotPassword(
            new ForgotPasswordRequest { Email = email, Metadata = metadata });

        result.IsSuccess.Should().BeTrue(
            "forgot-password must not disclose account existence or delivery state");

        var verification = await WaitForPasswordResetVerificationAsync(credential.Id);
        verification.Status.Should().Be((short)GenericStatusType.Canceled);
        verification.ConsumedAt.Should().NotBeNull();
        verification.IsEnabled.Should().BeFalse();

        await using var db = CreateDbContext();
        var resetOutbox = await db.Set<PasswordResetOutboxMessage>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(message =>
                message.TenantId == IntegrationTestFixture.TestTenantId &&
                message.RequestId == metadata.RequestId);
        resetOutbox.ProcessedAt.Should().NotBeNull();
        resetOutbox.Email.Should().BeNull();
        resetOutbox.Phone.Should().BeNull();

        var deliveryOutbox = await db.Set<VerificationDeliveryOutboxMessage>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(message => message.VerificationId == verification.Id);
        deliveryOutbox.DeadLetteredAt.Should().NotBeNull();
        deliveryOutbox.Attempts.Should().Be(1);
        deliveryOutbox.Recipient.Should().BeNull();
        deliveryOutbox.Message.Should().BeNull();
        IdentityServerWorkflowFailureInjection.CommunicationsDeliveryAttemptCount.Should().Be(1,
            "an ambiguous downstream outcome must never be retried");
    }

    [Test]
    public async Task ForgotPassword_AcceptedRequest_IsDurablyDispatchedAndPurgesContactData()
    {
        var requestId = Guid.NewGuid();
        var result = await IntegrationTestFixture.ServiceWrapper.ForgotPassword(
            new ForgotPasswordRequest
            {
                Email = $"missing-{Guid.NewGuid():N}@example.test",
                Metadata = new RequestMetadata
                {
                    RequestedTenantId = IntegrationTestFixture.TestTenantId,
                    RequestId = requestId,
                    IpAddress = "127.0.0.1"
                }
            });

        result.IsSuccess.Should().BeTrue();
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            await using var db = CreateDbContext();
            var outbox = await db.Set<PasswordResetOutboxMessage>()
                .IgnoreQueryFilters()
                .AsNoTracking()
                .SingleOrDefaultAsync(message =>
                    message.TenantId == IntegrationTestFixture.TestTenantId &&
                    message.RequestId == requestId);
            if (outbox?.ProcessedAt is not null)
            {
                outbox.Email.Should().BeNull();
                outbox.Phone.Should().BeNull();
                return;
            }

            await Task.Delay(100);
        }

        throw new AssertionException("Password reset outbox message was not durably dispatched within 10 seconds.");
    }

    [Test]
    public async Task ForgotPassword_WhenProcessorRejectsOnce_RetriesAndProcesses()
    {
        IdentityServerWorkflowFailureInjection.FailNextPasswordResetProcessing();
        var requestId = Guid.NewGuid();

        var result = await IntegrationTestFixture.ServiceWrapper.ForgotPassword(new ForgotPasswordRequest
        {
            Email = $"missing-{Guid.NewGuid():N}@example.test",
            Metadata = new RequestMetadata
            {
                RequestedTenantId = IntegrationTestFixture.TestTenantId,
                RequestId = requestId,
                IpAddress = "127.0.0.1"
            }
        });

        result.IsSuccess.Should().BeTrue();
        var outbox = await WaitForPasswordResetOutboxAsync(requestId, message => message.ProcessedAt is not null);
        outbox.Attempts.Should().Be(2);
        outbox.DeadLetteredAt.Should().BeNull();
        outbox.Email.Should().BeNull();
    }

    [Test]
    public async Task ForgotPassword_WhenCommunicationsRejectsOnce_RetriesDeliveryWithoutCancelingVerification()
    {
        var (credential, email) = await SeedCredentialWithEmailAsync();
        IdentityServerWorkflowFailureInjection.RejectNextCommunicationsDeliveries(1);
        var metadata = CreateMetadata(targetTenant: true);

        var result = await IntegrationTestFixture.ServiceWrapper.ForgotPassword(
            new ForgotPasswordRequest { Email = email, Metadata = metadata });

        result.IsSuccess.Should().BeTrue();
        var verification = await WaitForPasswordResetVerificationCreatedAsync(credential.Id);
        var delivery = await WaitForVerificationDeliveryAsync(
            verification.Id,
            message => message.ProcessedAt is not null);
        delivery.Attempts.Should().Be(2);
        delivery.DeadLetteredAt.Should().BeNull();
        delivery.Recipient.Should().BeNull();
        IdentityServerWorkflowFailureInjection.CommunicationsDeliveryAttemptCount.Should().BeGreaterThanOrEqualTo(2);

        await using var db = CreateDbContext();
        var persistedVerification = await db.Set<IdentityVerification>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(item => item.Id == verification.Id);
        persistedVerification.Status.Should().Be((short)GenericStatusType.Pending);
        persistedVerification.ConsumedAt.Should().BeNull();
        persistedVerification.IsEnabled.Should().BeTrue();
    }

    [Test]
    public async Task PasswordResetOutbox_ExpiredPreDispatchLease_IsRecoveredAndProcessed()
    {
        var requestId = Guid.NewGuid();
        await using (var db = CreateDbContext())
        {
            db.Set<PasswordResetOutboxMessage>().Add(new PasswordResetOutboxMessage
            {
                Id = Guid.NewGuid(),
                TenantId = IntegrationTestFixture.TestTenantId,
                RequestId = requestId,
                Email = $"missing-{Guid.NewGuid():N}@example.test",
                Attempts = 1,
                LeaseOwner = "stopped-worker",
                LeaseExpiresAt = DateTime.UtcNow.AddMinutes(-1),
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow.AddMinutes(-2),
                ConcurrencyStamp = Guid.NewGuid()
            });
            await db.SaveChangesAsync();
        }

        var outbox = await WaitForPasswordResetOutboxAsync(requestId, message => message.ProcessedAt is not null);
        outbox.Attempts.Should().Be(2);
        outbox.DeadLetteredAt.Should().BeNull();
    }

    [Test]
    public async Task VerificationDeliveryOutbox_ExpiredPreDispatchLease_IsRecoveredAndProcessed()
    {
        var credential = await SeedCredentialAsync();
        var verificationId = Guid.NewGuid();
        await using (var db = CreateDbContext())
        {
            db.Set<IdentityVerification>().Add(new IdentityVerification
            {
                Id = verificationId,
                TenantId = IntegrationTestFixture.TestTenantId,
                CredentialId = credential.Id,
                VerificationTypeId = IdentityConstants.VerificationType.Email,
                Purpose = IdentityConstants.VerificationPurpose.PasswordReset,
                TokenHash = new string('a', 64),
                Expiry = DateTime.UtcNow.AddMinutes(10),
                Status = (short)GenericStatusType.Pending,
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                ConcurrencyStamp = Guid.NewGuid()
            });
            db.Set<VerificationDeliveryOutboxMessage>().Add(new VerificationDeliveryOutboxMessage
            {
                Id = Guid.NewGuid(),
                TenantId = IntegrationTestFixture.TestTenantId,
                VerificationId = verificationId,
                RequestId = Guid.NewGuid(),
                TransportType = (int)MessageTransportType.Email,
                Recipient = $"lease-{Guid.NewGuid():N}@example.test",
                Subject = "Lease recovery",
                Intent = "Verification",
                Message = "Recovery test",
                Attempts = 1,
                LeaseOwner = "stopped-worker",
                LeaseExpiresAt = DateTime.UtcNow.AddMinutes(-1),
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow.AddMinutes(-2),
                ConcurrencyStamp = Guid.NewGuid()
            });
            await db.SaveChangesAsync();
        }

        var delivery = await WaitForVerificationDeliveryAsync(
            verificationId,
            message => message.ProcessedAt is not null);
        delivery.Attempts.Should().Be(2);
        delivery.DeadLetteredAt.Should().BeNull();
    }

    [Test]
    public async Task ForgotPassword_ConcurrentSameRequestId_ReturnsSameAcceptedOutcomeAndPersistsOnce()
    {
        var requestId = Guid.NewGuid();
        var email = $"concurrent-{Guid.NewGuid():N}@example.test";
        ForgotPasswordRequest CreateRequest() => new()
        {
            Email = email,
            Metadata = new RequestMetadata
            {
                RequestedTenantId = IntegrationTestFixture.TestTenantId,
                RequestId = requestId,
                IpAddress = "127.0.0.1"
            }
        };

        var results = await Task.WhenAll(
            IntegrationTestFixture.ServiceWrapper.ForgotPassword(CreateRequest()),
            IntegrationTestFixture.ServiceWrapper.ForgotPassword(CreateRequest()));

        results.Should().OnlyContain(result => result.IsSuccess);
        await using var db = CreateDbContext();
        var count = await db.Set<PasswordResetOutboxMessage>()
            .IgnoreQueryFilters()
            .CountAsync(message =>
                message.TenantId == IntegrationTestFixture.TestTenantId &&
                message.RequestId == requestId);
        count.Should().Be(1);
    }

    private async Task<IdentityVerification> WaitForPasswordResetVerificationAsync(Guid credentialId)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            await using var db = CreateDbContext();
            var verification = await db.Set<IdentityVerification>()
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(item => item.CredentialId == credentialId)
                .Where(item => item.Purpose == IdentityConstants.VerificationPurpose.PasswordReset)
                .OrderByDescending(item => item.CreatedAt)
                .FirstOrDefaultAsync();
            if (verification is { IsEnabled: false, ConsumedAt: not null })
                return verification;

            await Task.Delay(100);
        }

        throw new AssertionException("Password reset dispatch did not complete within 10 seconds.");
    }

    private async Task<IdentityVerification> WaitForPasswordResetVerificationCreatedAsync(Guid credentialId)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            await using var db = CreateDbContext();
            var verification = await db.Set<IdentityVerification>()
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(item => item.CredentialId == credentialId)
                .Where(item => item.Purpose == IdentityConstants.VerificationPurpose.PasswordReset)
                .OrderByDescending(item => item.CreatedAt)
                .FirstOrDefaultAsync();
            if (verification is not null)
                return verification;

            await Task.Delay(100);
        }

        throw new AssertionException("Password reset verification was not created within 15 seconds.");
    }

    private async Task<PasswordResetOutboxMessage> WaitForPasswordResetOutboxAsync(
        Guid requestId,
        Func<PasswordResetOutboxMessage, bool> completed)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            await using var db = CreateDbContext();
            var outbox = await db.Set<PasswordResetOutboxMessage>()
                .IgnoreQueryFilters()
                .AsNoTracking()
                .SingleOrDefaultAsync(message =>
                    message.TenantId == IntegrationTestFixture.TestTenantId &&
                    message.RequestId == requestId);
            if (outbox is not null && completed(outbox))
                return outbox;

            await Task.Delay(100);
        }

        throw new AssertionException("Password reset outbox did not reach the expected state within 15 seconds.");
    }

    private async Task<VerificationDeliveryOutboxMessage> WaitForVerificationDeliveryAsync(
        Guid verificationId,
        Func<VerificationDeliveryOutboxMessage, bool> completed)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            await using var db = CreateDbContext();
            var outbox = await db.Set<VerificationDeliveryOutboxMessage>()
                .IgnoreQueryFilters()
                .AsNoTracking()
                .SingleOrDefaultAsync(message => message.VerificationId == verificationId);
            if (outbox is not null && completed(outbox))
                return outbox;

            await Task.Delay(100);
        }

        throw new AssertionException("Verification delivery outbox did not reach the expected state within 15 seconds.");
    }

    private static async Task<int> WaitForAbortCountAsync()
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            var count = IdentityServerWorkflowFailureInjection.AbortedUploadCount;
            if (count > 0)
            {
                await Task.Delay(100);
                return IdentityServerWorkflowFailureInjection.AbortedUploadCount;
            }

            await Task.Delay(50);
        }

        return IdentityServerWorkflowFailureInjection.AbortedUploadCount;
    }

    private async Task<IdentityCredential> SeedCredentialAsync()
    {
        await using var db = CreateDbContext();
        var identity = new IdentityInformation
        {
            Id = Guid.NewGuid(),
            TenantId = IntegrationTestFixture.TestTenantId,
            FirstName = "Workflow",
            LastName = "Compensation",
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            ConcurrencyStamp = Guid.NewGuid()
        };
        var credential = new IdentityCredential
        {
            Id = Guid.NewGuid(),
            TenantId = IntegrationTestFixture.TestTenantId,
            IdentityInfoId = identity.Id,
            UserName = UniqueUsername(),
            PasswordByte = Encoding.ASCII.GetBytes(
                BCrypt.Net.BCrypt.HashPassword("WorkflowPassword123!", workFactor: 11)),
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            ConcurrencyStamp = Guid.NewGuid()
        };
        db.Add(identity);
        db.Add(credential);
        db.Add(new IdentityRole
        {
            Id = Guid.NewGuid(),
            TenantId = IntegrationTestFixture.TestTenantId,
            CredentialId = credential.Id,
            TypeId = TestData.RoleTypeId,
            RoleExpiration = DateTime.UtcNow.AddYears(1),
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            ConcurrencyStamp = Guid.NewGuid()
        });
        await db.SaveChangesAsync();
        return credential;
    }

    private async Task<(IdentityCredential Credential, string Email)> SeedCredentialWithEmailAsync()
    {
        var credential = await SeedCredentialAsync();
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
                Name = "Workflow contacts",
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

    private static UploadCredentialAvatarRequest CreateAvatarRequest(Guid credentialId) => new()
    {
        CredentialId = credentialId,
        FileName = "workflow-avatar.png",
        ContentType = "image/png",
        FileBytes = [137, 80, 78, 71, 13, 10, 26, 10],
        Metadata = CreateMetadata()
    };

    private static RequestMetadata CreateMetadata(bool targetTenant = false) => new()
    {
        RequestedTenantId = targetTenant ? IntegrationTestFixture.TestTenantId : null,
        RequestId = Guid.NewGuid(),
        IpAddress = "127.0.0.1",
        OperationName = nameof(WorkflowCompensationTests),
        DeviceName = "TestDevice",
        UserAgent = "TestAgent"
    };
}
