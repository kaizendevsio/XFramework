using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.JSInterop;
using Moq;
using NUnit.Framework;
using Yap.Client.Services;
using Yap.Contracts;

namespace Yap.Client.Tests;

public sealed class ChatEncryptionTests
{
    [Test]
    public async Task EnsureAsync_RepairsMissingBackup_WithoutRevealingRecoveryKey()
    {
        using var fixture = new Fixture();
        await fixture.Encryption.EnsureAsync(fixture.User);
        Assert.Multiple(() =>
        {
            Assert.That(fixture.BackupPosts, Is.EqualTo(1));
            Assert.That(fixture.Encryption.Status.Approved, Is.True);
            Assert.That(fixture.Encryption.RecoveryKey, Is.Null);
        });
        await fixture.Encryption.ShowRecoveryAsync(fixture.User);
        Assert.That(fixture.Encryption.RecoveryKey, Is.EqualTo("recovery-secret"));
        fixture.Encryption.HideRecovery();
        Assert.That(fixture.Encryption.RecoveryKey, Is.Null);
    }

    [Test]
    public async Task ShowRecoveryAsync_AccountChangesWhileExporting_DoesNotPublishOrRevealOldKeys()
    {
        using var fixture = new Fixture();
        await fixture.Encryption.EnsureAsync(fixture.User);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var result = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Js.Setup(j => j.InvokeAsync<JsonElement>("yap.encryption.exportRecovery", It.IsAny<object?[]?>()))
            .Returns(() => { started.TrySetResult(); return new ValueTask<JsonElement>(result.Task); });
        var reveal = fixture.Encryption.ShowRecoveryAsync(fixture.User);
        await started.Task;
        fixture.Encryption.Reset(); fixture.Api.Account = "another-account";
        result.SetResult(Fixture.Json(new { recoveryKey = "old-secret", recoveryArchive = "old-archive" }));
        Assert.ThrowsAsync<OperationCanceledException>(async () => await reveal);
        Assert.Multiple(() =>
        {
            Assert.That(fixture.Encryption.RecoveryKey, Is.Null);
            Assert.That(fixture.Encryption.Status.Approved, Is.False);
            Assert.That(fixture.BackupPosts, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task EnsureAsync_AccountChangesWhileDirectoryIsLoading_DoesNotInvokeOldAccountCrypto()
    {
        using var fixture = new Fixture();
        var result = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.OverrideGet = () => result.Task;
        var ensure = fixture.Encryption.EnsureAsync(fixture.User);
        fixture.Encryption.Reset(); fixture.Api.Account = "another-account";
        result.SetResult(new(HttpStatusCode.OK) { Content = JsonContent.Create(fixture.Directory) });
        Assert.ThrowsAsync<OperationCanceledException>(async () => await ensure);
        fixture.Js.Verify(j => j.InvokeAsync<ChatEncryption.EncryptionStatus>("yap.encryption.status", It.IsAny<object?[]?>()), Times.Never);
        Assert.That(fixture.Encryption.Status.Approved, Is.False);
    }

    [Test]
    public async Task RestoreAsync_DirectoryPostResponseIsLost_ConfirmsExistingWriteAndKeepsBackupPending()
    {
        using var fixture = new Fixture();
        var restored = fixture.NewDirectory(2);
        fixture.Backup = Fixture.Json(new { revision = 1, archive = "previous-backup" });
        fixture.LoseDirectoryResponse = true;
        fixture.FailBackup = true;
        fixture.InitiallyApproved = false;
        fixture.Js.Setup(j => j.InvokeAsync<JsonElement>("yap.encryption.recovery", It.IsAny<object?[]?>()))
            .ReturnsAsync(Fixture.Json(new { directory = restored, recoveryArchive = "restored-backup", recoveryKey = "recovery-secret" }));
        await fixture.Encryption.RestoreAsync(fixture.User, "recovery-secret");
        Assert.Multiple(() =>
        {
            Assert.That(fixture.DirectoryPosts, Is.EqualTo(1));
            Assert.That(fixture.Directory.GetProperty("revision").GetInt64(), Is.EqualTo(2));
            Assert.That(fixture.Encryption.Status.Approved, Is.True);
            Assert.That(fixture.Encryption.BackupPending, Is.True);
            Assert.That(fixture.Encryption.RecoveryKey, Is.Null);
        });
        fixture.FailBackup = false;
        await fixture.Encryption.EnsureAsync(fixture.User);
        Assert.That(fixture.Encryption.BackupPending, Is.False);
        Assert.That(fixture.DirectoryPosts, Is.EqualTo(1), "Backup retry must not repeat device recovery.");
    }

    [Test]
    public async Task RestoreAsync_PreviousRestoreAlreadyConfirmed_RepairsBackupWithoutRotatingAgain()
    {
        using var fixture = new Fixture();
        await fixture.Encryption.RestoreAsync(fixture.User, "saved-secret");
        Assert.That(fixture.DirectoryPosts, Is.Zero);
        fixture.Js.Verify(j => j.InvokeAsync<JsonElement>("yap.encryption.recovery", It.IsAny<object?[]?>()), Times.Never);
        Assert.That(fixture.Encryption.Status.Approved, Is.True);
        Assert.That(fixture.BackupPosts, Is.EqualTo(1));
    }

    [Test]
    public async Task HideRecovery_PendingRevealCompletesLater_SecretRemainsHidden()
    {
        using var fixture = new Fixture(); await fixture.Encryption.EnsureAsync(fixture.User);
        var result = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Js.Setup(j => j.InvokeAsync<JsonElement>("yap.encryption.exportRecovery", It.IsAny<object?[]?>()))
            .Returns(new ValueTask<JsonElement>(result.Task));
        var reveal = fixture.Encryption.ShowRecoveryAsync(fixture.User);
        fixture.Encryption.HideRecovery();
        result.SetResult(Fixture.Json(new { recoveryKey = "hidden-secret", recoveryArchive = "archive" }));
        await reveal;
        Assert.That(fixture.Encryption.RecoveryKey, Is.Null);
    }

    [Test]
    public async Task PasswordRestoreAsync_UnusableBackup_SurfacesTheReasonInsteadOfAFalseResult()
    {
        using var fixture = new Fixture();
        await fixture.Encryption.EnsureAsync(fixture.User);
        const string reason = "Your password no longer opens this account's saved backup.";
        fixture.Js.Setup(j => j.InvokeAsync<bool>("yap.encryption.passwordRestore", It.IsAny<object?[]?>()))
            .Throws(new JSException(reason));
        // The regression: this used to reach the settings page as a sentence about codes and
        // fingerprints, so nobody - user or log - learned what actually stopped the unlock.
        var error = Assert.ThrowsAsync<JSException>(() => fixture.Encryption.PasswordRestoreAsync(fixture.User));
        Assert.That(error!.Message, Is.EqualTo(reason));
    }

    [Test]
    public async Task PasswordUnlockStateAsync_ReportsWhatThisSignInStillHolds()
    {
        using var fixture = new Fixture();
        await fixture.Encryption.EnsureAsync(fixture.User);
        fixture.Js.Setup(j => j.InvokeAsync<ChatEncryption.PasswordUnlock>("yap.encryption.passwordUnlockState", It.IsAny<object?[]?>()))
            .ReturnsAsync(new ChatEncryption.PasswordUnlock(true, true, null));
        Assert.That(await fixture.Encryption.PasswordUnlockStateAsync(fixture.User), Is.EqualTo(new ChatEncryption.PasswordUnlock(true, true, null)));
        fixture.Js.Verify(j => j.InvokeAsync<ChatEncryption.PasswordUnlock>("yap.encryption.passwordUnlockState",
            It.Is<object?[]?>(a => a != null && a.Length == 1 && (string)a[0]! == OfflineStore.Scope(fixture.User))), Times.Once);
    }

    [Test]
    public async Task RevokeAsync_LocalDevice_IsRejectedBeforeDirectoryMutation()
    {
        using var fixture = new Fixture();
        await fixture.Encryption.EnsureAsync(fixture.User);
        Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Encryption.RevokeAsync(fixture.User, fixture.DeviceId));
        Assert.That(fixture.DirectoryPosts, Is.Zero);
    }

    [Test]
    public async Task VerifyFingerprintAsync_UsesComparedValueAndRevalidatesDirectory()
    {
        using var fixture = new Fixture();
        var peer = Guid.NewGuid();
        await fixture.Encryption.EnsureAsync(fixture.User);
        fixture.Js.Setup(j => j.InvokeAsync<bool>("yap.encryption.verifyDirectory", It.IsAny<object?[]?>())).ReturnsAsync(true);
        await fixture.Encryption.VerifyFingerprintAsync(fixture.User, peer, "the compared fingerprint");
        fixture.Js.Verify(j => j.InvokeAsync<bool>("yap.encryption.verifyDirectory", It.Is<object?[]?>(a => a != null
            && (string)a[0]! == OfflineStore.Scope(fixture.User) && ((JsonElement)a[1]!).GetProperty("roster").GetString() == "signed-roster-1"
            && (string)a[2]! == "the compared fingerprint")), Times.Once);
    }

    [Test]
    public async Task ResetIdentityAsync_LostResponseResumesConfirmationWithoutPublishingAgain()
    {
        using var fixture = new Fixture { LoseDirectoryResponse = true };
        await fixture.Encryption.EnsureAsync(fixture.User);
        var next = fixture.NewDirectory(2);
        fixture.Js.Setup(j => j.InvokeAsync<JsonElement>("yap.encryption.prepareReset", It.IsAny<object?[]?>()))
            .ReturnsAsync(Fixture.Json(new { directory = next, recoveryArchive = "encrypted-new-backup" }));
        fixture.Js.Setup(j => j.InvokeAsync<bool>("yap.encryption.confirmReset", It.IsAny<object?[]?>()))
            .Returns((string _, object?[]? args) => ValueTask.FromResult(((JsonElement)args![1]!).GetProperty("revision").GetInt64() == 2));
        await fixture.Encryption.ResetIdentityAsync(fixture.User, "fixture");
        await fixture.Encryption.ResetIdentityAsync(fixture.User, "fixture");
        Assert.That(fixture.ResetPosts, Is.EqualTo(1));
        Assert.That(fixture.Encryption.Status.Approved, Is.True);
        Assert.That(fixture.Encryption.RecoveryKey, Is.Null);
    }

    // Local keys can go missing for reasons that say nothing about the account: a cleared site, a
    // second browser, a sign-out. Answering that with a new account root is what orphaned history.
    [Test]
    public async Task EnsureAsync_AccountHasARecoveryArchiveButNoRoster_ReportsLockedAndCreatesNothing()
    {
        using var fixture = new Fixture { DirectoryMissing = true, Backup = Fixture.Json(new { revision = 1, archive = "encrypted-backup" }) };
        await fixture.Encryption.EnsureAsync(fixture.User);
        Assert.Multiple(() =>
        {
            Assert.That(fixture.DirectoryPosts, Is.Zero);
            Assert.That(fixture.Encryption.Locked, Does.Contain("recovery key"));
        });
        fixture.Js.Verify(j => j.InvokeAsync<JsonElement>("yap.encryption.initialize", It.IsAny<object?[]?>()), Times.Never);
    }

    [Test]
    public async Task EnsureAsync_ServerHasNeitherDirectoryNorArchive_EnrollsWithThatCheckedAnswer()
    {
        using var fixture = new Fixture { DirectoryMissing = true };
        fixture.Js.Setup(j => j.InvokeAsync<JsonElement>("yap.encryption.initialize", It.IsAny<object?[]?>()))
            .ReturnsAsync(Fixture.Json(new { directory = fixture.NewDirectory(1) }));
        await fixture.Encryption.EnsureAsync(fixture.User);
        Assert.Multiple(() =>
        {
            Assert.That(fixture.DirectoryPosts, Is.EqualTo(1));
            Assert.That(fixture.Encryption.Status.Approved, Is.True);
            Assert.That(fixture.Encryption.Locked, Is.Null);
        });
        // The account state travels with the call; enrolment is never inferred from missing keys.
        fixture.Js.Verify(j => j.InvokeAsync<JsonElement>("yap.encryption.initialize", It.Is<object?[]?>(a =>
            a != null && a.Length == 2 && Fixture.Json(a[1]!).GetProperty("checked").GetBoolean()
            && Fixture.Json(a[1]!).GetProperty("directory").ValueKind == JsonValueKind.Null
            && Fixture.Json(a[1]!).GetProperty("recoveryArchive").ValueKind == JsonValueKind.Null)), Times.Once);
    }

    [Test]
    public async Task EnsureAsync_DeviceCannotReadThePublishedIdentity_FinishesTheSignInUnlockFirst()
    {
        using var fixture = new Fixture();
        var unlocked = false;
        fixture.Js.Setup(j => j.InvokeAsync<ChatEncryption.EncryptionStatus>("yap.encryption.status", It.IsAny<object?[]?>()))
            .Returns(() => ValueTask.FromResult(new ChatEncryption.EncryptionStatus { Enrolled = unlocked, Approved = unlocked,
                CanApproveDevices = unlocked, DeviceId = fixture.DeviceId, RootFingerprint = unlocked ? "root" : null, DirectoryRevision = unlocked ? 2 : 0 }));
        fixture.Js.Setup(j => j.InvokeAsync<bool>("yap.encryption.passwordRestore", It.IsAny<object?[]?>()))
            .Returns(() => { unlocked = true; return ValueTask.FromResult(true); });
        await fixture.Encryption.EnsureAsync(fixture.User);
        Assert.Multiple(() =>
        {
            Assert.That(fixture.Encryption.Status.Approved, Is.True);
            Assert.That(fixture.Encryption.Locked, Is.Null);
        });
        // The unlock runs inside the same account lock that just read the directory, so its result
        // no longer depends on whether sign-in or synchronization reached the module first.
        fixture.Js.Verify(j => j.InvokeAsync<bool>("yap.encryption.passwordRestore", It.IsAny<object?[]?>()), Times.Once);
        fixture.Js.Verify(j => j.InvokeAsync<JsonElement>("yap.encryption.initialize", It.IsAny<object?[]?>()), Times.Never);
        fixture.Js.Verify(j => j.InvokeAsync<JsonElement>("yap.encryption.proposeDevice", It.IsAny<object?[]?>()), Times.Never);
    }

    [Test]
    public async Task EnsureAsync_PublishedIdentityAndNothingCanUnlockIt_ReportsLockedAndKeepsTheDirectory()
    {
        using var fixture = new Fixture();
        fixture.Js.Setup(j => j.InvokeAsync<ChatEncryption.EncryptionStatus>("yap.encryption.status", It.IsAny<object?[]?>()))
            .ReturnsAsync(new ChatEncryption.EncryptionStatus { Enrolled = false, Approved = false, RootFingerprint = null });
        fixture.Js.Setup(j => j.InvokeAsync<bool>("yap.encryption.passwordRestore", It.IsAny<object?[]?>())).ReturnsAsync(false);
        await fixture.Encryption.EnsureAsync(fixture.User);
        Assert.Multiple(() =>
        {
            Assert.That(fixture.DirectoryPosts, Is.Zero);
            Assert.That(fixture.Encryption.Status.Approved, Is.False);
            Assert.That(fixture.Encryption.Locked, Does.Contain("locked"));
        });
        fixture.Js.Verify(j => j.InvokeAsync<JsonElement>("yap.encryption.initialize", It.IsAny<object?[]?>()), Times.Never);
        // Still local-only, and still what makes approval from a trusted device possible.
        fixture.Js.Verify(j => j.InvokeAsync<JsonElement>("yap.encryption.proposeDevice", It.IsAny<object?[]?>()), Times.Once);
    }

    [Test]
    public void EnsureAsync_ServerUnreachable_WaitsInsteadOfAssumingTheAccountHasNoIdentity()
    {
        using var fixture = new Fixture { OverrideGet = () => Task.FromException<HttpResponseMessage>(new HttpRequestException("offline")) };
        Assert.ThrowsAsync<HttpRequestException>(() => fixture.Encryption.EnsureAsync(fixture.User));
        Assert.That(fixture.DirectoryPosts, Is.Zero);
        fixture.Js.Verify(j => j.InvokeAsync<JsonElement>("yap.encryption.initialize", It.IsAny<object?[]?>()), Times.Never);
    }

    private sealed class Fixture : IDisposable
    {
        public UserSession User { get; } = new(Guid.NewGuid(), Guid.NewGuid(), "Owner");
        public Guid DeviceId { get; } = Guid.NewGuid();
        public Mock<IJSRuntime> Js { get; } = new();
        public ChatApi Api { get; }
        public ChatEncryption Encryption { get; }
        public JsonElement Directory { get; private set; }
        public JsonElement? Backup { get; set; }
        public int BackupPosts { get; private set; }
        public int DirectoryPosts { get; private set; }
        public int ResetPosts { get; private set; }
        public bool LoseDirectoryResponse { get; set; }
        public bool DirectoryMissing { get; set; }
        public bool FailBackup { get; set; }
        public bool InitiallyApproved { get; set; } = true;
        public Func<Task<HttpResponseMessage>>? OverrideGet { get; set; }
        private readonly HttpClient http;
        public Fixture()
        {
            Directory = NewDirectory(1);
            http = new(new Handler(Handle)) { BaseAddress = new("https://yap.test/") };
            Api = new(http) { Account = OfflineStore.Scope(User) };
            Encryption = new(Api, Js.Object);
            Js.Setup(j => j.InvokeAsync<ChatEncryption.EncryptionStatus>("yap.encryption.status", It.IsAny<object?[]?>()))
                .Returns(() => ValueTask.FromResult(new ChatEncryption.EncryptionStatus { Enrolled = true, Approved = InitiallyApproved || Directory.GetProperty("revision").GetInt64() > 1,
                    CanApproveDevices = InitiallyApproved || Directory.GetProperty("revision").GetInt64() > 1, DeviceId = DeviceId, RootFingerprint = "root", DirectoryRevision = Directory.GetProperty("revision").GetInt64() }));
            Js.Setup(j => j.InvokeAsync<JsonElement>("yap.encryption.exportRecovery", It.IsAny<object?[]?>()))
                .ReturnsAsync(Json(new { recoveryKey = "recovery-secret", recoveryArchive = "encrypted-backup" }));
        }
        public JsonElement NewDirectory(long revision) => Json(new { tenantId = User.TenantId, credentialId = User.CredentialId,
            revision, rootPublicKey = "root-key", roster = $"signed-roster-{revision}",
            devices = new[] { new { deviceId = DeviceId, revocation = (string?)null } } });
        public static JsonElement Json(object value) => JsonSerializer.SerializeToElement(value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        private async Task<HttpResponseMessage> Handle(HttpRequestMessage request)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Post && path.EndsWith("/reset"))
            {
                ResetPosts++; Directory = NewDirectory(2);
                if (LoseDirectoryResponse) throw new HttpRequestException("Response lost after reset");
                return new(HttpStatusCode.OK) { Content = JsonContent.Create(Directory) };
            }
            if (request.Method == HttpMethod.Get && (path.EndsWith("/directory") || path.Contains("/people/")))
            {
                if (OverrideGet is not null) return await OverrideGet();
                if (DirectoryMissing) return new(HttpStatusCode.NotFound);
                return new(HttpStatusCode.OK) { Content = JsonContent.Create(Directory) };
            }
            if (request.Method == HttpMethod.Post && path.EndsWith("/directory"))
            {
                DirectoryPosts++; DirectoryMissing = false;
                var sent = await request.Content!.ReadFromJsonAsync<JsonElement>();
                Directory = Json(new { tenantId = User.TenantId, credentialId = User.CredentialId,
                    revision = sent.GetProperty("expectedRevision").GetInt64() + 1, rootPublicKey = sent.GetProperty("rootPublicKey").GetString(),
                    roster = sent.GetProperty("roster").GetString(), devices = sent.GetProperty("devices") });
                if (LoseDirectoryResponse) throw new HttpRequestException("Response lost after saving");
                return new(HttpStatusCode.OK) { Content = JsonContent.Create(Directory) };
            }
            if (path.EndsWith("/recovery") && request.Method == HttpMethod.Get)
                return Backup is { } backup ? new(HttpStatusCode.OK) { Content = JsonContent.Create(backup) } : new(HttpStatusCode.NotFound);
            if (path.EndsWith("/recovery") && request.Method == HttpMethod.Post)
            {
                BackupPosts++;
                if (FailBackup) return new(HttpStatusCode.ServiceUnavailable);
                Backup = Json(new { revision = BackupPosts, archive = "encrypted-backup" });
                return new(HttpStatusCode.NoContent);
            }
            return new(HttpStatusCode.NotFound);
        }
        public void Dispose() => http.Dispose();
    }
    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request); }
}
