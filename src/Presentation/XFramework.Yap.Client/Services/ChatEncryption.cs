using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.JSInterop;
using Yap.Contracts;

namespace Yap.Client.Services;

/// <summary>Moves public directories and ciphertext only. Private keys stay in the browser crypto module.</summary>
public sealed class ChatEncryption(ChatApi api, IJSRuntime js, TimeProvider? timeProvider = null)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly object recipientsGate = new();
    // Public, signature-verified sending rosters only. Account changes clear this instance's cache.
    private readonly Dictionary<(Guid Thread, bool AllowPending), RecipientRoster> recipients = [];
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;
    private static readonly TimeSpan RecipientRosterLifetime = TimeSpan.FromSeconds(15);
    private long recipientVersion;
    private sealed class RecipientRoster
    {
        public Task<JsonElement[]> Loading { get; set; } = null!;
        public DateTimeOffset ExpiresAt { get; set; }
        public bool Invalidated { get; set; }
        public long Version { get; set; }
    }
    private sealed class RecipientRosterChangedException : Exception;
    private string? activeScope;
    private long generation;
    private long revealGeneration;
    private long backedUpRevision = -1;
    public bool BackupPending { get; private set; }
    public EncryptionStatus Status { get; private set; } = new();
    public string? RecoveryKey { get; private set; }
    /// <summary>Why this device cannot read the account's encrypted history, when it cannot.
    /// A locked device that keeps its history is the correct outcome; replacing the account
    /// identity to make the app look ready would orphan every message already sent to it.</summary>
    public string? Locked { get; private set; }
    private const string LockedMessage = "Your encrypted messages are locked on this device. Unlock with your password, use your recovery key, or approve this device from a device you already use.";
    /// <summary>The account publishes an identity this device cannot prove is its own. That is a
    /// decision - take the published identity, or restore the previous one - not a sentence to read,
    /// so settings offers both instead of a warning with nothing attached to it.</summary>
    public bool IdentityReplaced { get; private set; }
    public Guid? LocalDeviceId => Status.DeviceId;
    public void Reset() { generation++; activeScope = null; InvalidateRecipients(); HideRecovery(); Status = new(); Locked = null; IdentityReplaced = false; backedUpRevision = -1; BackupPending = false; }
    public void InvalidateRecipients(Guid? thread = null)
    {
        lock (recipientsGate)
        {
            recipientVersion++;
            foreach (var key in recipients.Keys.Where(x => thread is null || x.Thread == thread).ToArray())
            { recipients[key].Invalidated = true; recipients.Remove(key); }
            // Preserve unrelated completed entries, while fencing any evicted in-flight load.
            foreach (var roster in recipients.Values.Where(x => x.Loading.IsCompletedSuccessfully)) roster.Version = recipientVersion;
        }
    }
    public void HideRecovery() { revealGeneration++; RecoveryKey = null; }
    private sealed record Operation(string Scope, long Generation);
    private Operation Begin(UserSession user)
    {
        var scope = OfflineStore.Scope(user);
        if (api.Account != scope) throw new OperationCanceledException("The signed-in account changed.");
        if (activeScope != scope) { Reset(); activeScope = scope; }
        return new(scope, generation);
    }
    private void Check(Operation operation)
    {
        if (operation.Generation != generation || activeScope != operation.Scope || api.Account != operation.Scope)
            throw new OperationCanceledException("The signed-in account changed.");
    }
    private async Task<T> GetAsync<T>(Operation operation, string path)
    { Check(operation); var result = await api.GetAsync<T>(path); Check(operation); return result; }
    private async Task<T> PostAsync<T>(Operation operation, string path, object body)
    { Check(operation); var result = await api.PostAsync<T>(path, body); Check(operation); return result!; }
    private async Task<T> JsAsync<T>(Operation operation, string method, params object?[] arguments)
    { Check(operation); var result = await js.InvokeAsync<T>($"yap.encryption.{method}", [operation.Scope, .. arguments]); Check(operation); return result; }
    private async Task JsVoidAsync(Operation operation, string method, params object?[] arguments)
    { Check(operation); await js.InvokeVoidAsync($"yap.encryption.{method}", [operation.Scope, .. arguments]); Check(operation); }
    private async Task<T> ChangeAsync<T>(UserSession user, Func<Operation, Task<T>> action)
    {
        await gate.WaitAsync();
        try { InvalidateRecipients(); return await action(Begin(user)); }
        finally { InvalidateRecipients(); gate.Release(); }
    }

    public Task<bool> PasswordRestoreAsync(UserSession user) => ChangeAsync(user, async operation =>
    {
        var restored = await JsAsync<bool>(operation, "passwordRestore");
        Status = await JsAsync<EncryptionStatus>(operation, "status");
        if (Status.Approved) Locked = null;
        backedUpRevision = -1;
        return restored;
    });
    public Task<bool> PasswordEnrollAsync(UserSession user, string username, string password) => ChangeAsync(user,
        operation => JsAsync<bool>(operation, "passwordEnroll", username, password));
    /// <summary>What the account publishes against what this device can prove it holds.</summary>
    public Task<OwnIdentity> OwnIdentityAsync(UserSession user) => ChangeAsync(user, async operation =>
        await JsAsync<OwnIdentity>(operation, "ownIdentity", await GetAsync<JsonElement>(operation, "api/chat/encryption/directory")));
    public sealed record OwnIdentity(string? Published, string? Held, long Revision, bool Matches, bool Replaced);
    /// <summary>Takes the account's current published identity in place of the retired one this
    /// device holds, then finishes the unlock the sign-in already paid for, so the choice ends in a
    /// working device rather than another screen. Nothing is deleted: the previous root and its
    /// history keys stay, so restoring that identity later remains possible.</summary>
    public Task<bool> AdoptIdentityAsync(UserSession user) => ChangeAsync(user, async operation =>
    {
        var directory = await GetAsync<JsonElement>(operation, "api/chat/encryption/directory");
        await JsAsync<bool>(operation, "adoptIdentity", directory);
        var problem = await UnlockAsync(operation);
        Status = await JsAsync<EncryptionStatus>(operation, "status");
        backedUpRevision = -1;
        if (!Status.Approved) { Locked = problem ?? LockedMessage; return false; }
        Locked = null; IdentityReplaced = false;
        return true;
    });
    /// <summary>Whether the sign-in that is still held in this browser session can unlock history
    /// without asking for the password again, and why not when it cannot.</summary>
    public Task<PasswordUnlock> PasswordUnlockStateAsync(UserSession user) =>
        ChangeAsync(user, operation => JsAsync<PasswordUnlock>(operation, "passwordUnlockState"));
    public sealed record PasswordUnlock(bool SignedIn, bool Available, string? Problem);

    public static string CallRosterBinding(YapGroupCall call)
    {
        var roster = string.Join("|", call.Participants.Where(x => x.Accepted && !x.Left)
            .OrderBy(x => x.CredentialId).Select(x => $"{x.CredentialId:D}:{x.DeviceId:D}"));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"{call.Id:D}|{call.Revision}|{roster}")));
    }
    private static object CallContext(UserSession user, YapGroupCall call, long sequence, Guid senderId,
        Guid senderDeviceId, Guid recipientId, string kind) => new
    {
        tenantId = user.TenantId.ToString(), threadId = call.ThreadId.ToString(), messageId = call.Id.ToString(), senderId = senderId.ToString(),
        expectedSenderDeviceId = senderDeviceId.ToString(), kind = "call-control", callId = call.Id.ToString(), epoch = call.Revision,
        sequence, recipientId = recipientId.ToString(), controlKind = kind, rosterBinding = CallRosterBinding(call)
    };
    public async Task<string> EncryptCallControlAsync(UserSession user, YapGroupCall call, long sequence,
        Guid recipientId, string kind, object payload)
    {
        var operation = Begin(user);
        var recipient = call.Participants.Single(x => x.CredentialId == recipientId && x.Accepted && !x.Left);
        var local = call.Participants.Single(x => x.CredentialId == user.CredentialId && x.Accepted && !x.Left);
        if (local.DeviceId != Status.DeviceId) throw new InvalidOperationException("This call belongs to a different device.");
        var directories = await RecipientsAsync(user, call.ThreadId);
        return await JsAsync<string>(operation, "encryptToDevices",
            CallContext(user, call, sequence, user.CredentialId, local.DeviceId, recipientId, kind), payload,
            directories.Where(x => x.GetProperty("credentialId").GetGuid() == user.CredentialId || x.GetProperty("credentialId").GetGuid() == recipientId).ToArray(),
            new[] { local.DeviceId.ToString(), recipient.DeviceId.ToString() }.Distinct().ToArray());
    }
    public async Task<JsonElement> DecryptCallControlAsync(UserSession user, YapGroupCall call, YapGroupControlEvent control)
    {
        var operation = Begin(user);
        if (control.CallId != call.Id || control.Revision != call.Revision || control.RecipientId != user.CredentialId
            || !call.Participants.Any(x => x.Accepted && !x.Left && x.CredentialId == control.SenderId && x.DeviceId == control.SenderDeviceId))
            throw new InvalidOperationException("Call membership changed.");
        var sender = await GetAsync<JsonElement>(operation, $"api/chat/encryption/people/{control.SenderId}");
        if (!sender.GetProperty("devices").EnumerateArray().Any(x => x.GetProperty("deviceId").GetGuid() == control.SenderDeviceId
                && (!x.TryGetProperty("revocation", out var revocation) || revocation.ValueKind == JsonValueKind.Null)))
            throw new InvalidOperationException("The calling device was revoked.");
        return await JsAsync<JsonElement>(operation, "decrypt",
            CallContext(user, call, control.Sequence, control.SenderId, control.SenderDeviceId, user.CredentialId, control.Kind), control.Envelope, sender);
    }

    public Task EnsureAsync(UserSession user) => ChangeAsync(user, async operation =>
    {
            // What the account already has decides everything below. An answer this request could
            // not obtain - offline, 5xx, a rejected session - propagates instead of being read as
            // "no identity": signing in again must never be able to mint a replacement root.
            JsonElement directory;
            try { directory = await GetAsync<JsonElement>(operation, "api/chat/encryption/directory"); }
            catch (ChatApiException ex) when (ex.Status == 404)
            {
                // No published roster. A stored archive still means this account has an identity
                // whose history a new root would orphan, so enrolment needs the server to hold neither.
                string? archive = null;
                try { archive = (await GetAsync<JsonElement>(operation, "api/chat/encryption/recovery")).GetProperty("archive").GetString(); }
                catch (ChatApiException missing) when (missing.Status == 404) { }
                if (archive is not null)
                {
                    Status = await JsAsync<EncryptionStatus>(operation, "status");
                    Locked = "This account has an encrypted backup but no registered devices. Restore it with your recovery key, or approve this device from a device you already use.";
                    return true;
                }
                var enrollment = await JsAsync<JsonElement>(operation, "initialize",
                    new { kind = "account-identity", @checked = true, directory = (object?)null, recoveryArchive = (string?)null });
                directory = await PublishDirectoryAsync(operation, enrollment.GetProperty("directory"));
                await JsVoidAsync(operation, "acceptDirectory", directory);
            }
            await JsAsync<bool>(operation, "confirmReset", directory);
            if (await JsAsync<bool>(operation, "observeOwnDirectory", directory))
            {
                Status = await JsAsync<EncryptionStatus>(operation, "status");
                HideRecovery();
                IdentityReplaced = true;
                Locked = "This account's encryption identity was replaced. Take the account's current identity, or restore your previous one with its recovery key or a device that still has it.";
                return true;
            }
            var local = await JsAsync<EncryptionStatus>(operation, "status");
            if (!local.Enrolled || local.RootFingerprint is null)
            {
                // This device cannot read the published identity yet. The unlock that sign-in
                // already paid for runs here, inside the same account lock that just read the
                // directory, so the outcome no longer depends on which caller ran first.
                var problem = await UnlockAsync(operation);
                local = await JsAsync<EncryptionStatus>(operation, "status");
                if (problem is null && local.Approved) directory = await GetAsync<JsonElement>(operation, "api/chat/encryption/directory");
                if (!local.Enrolled)
                {
                    await JsAsync<JsonElement>(operation, "proposeDevice");
                    local = await JsAsync<EncryptionStatus>(operation, "status");
                }
                if (local.RootFingerprint is null) { Status = local; Locked = problem ?? LockedMessage; return true; }
            }
            Locked = null; IdentityReplaced = false;
            await JsVoidAsync(operation, "acceptDirectory", directory);
            Status = await JsAsync<EncryptionStatus>(operation, "status");
            if (Status.CanApproveDevices && backedUpRevision != Status.DirectoryRevision)
            {
                // Repair setup interrupted after directory publication. A failed backup write
                // must not discard the local keys or prevent reading existing conversations.
                try
                {
                    await SaveBackupAsync(operation, reveal: false);
                }
                catch { Check(operation); BackupPending = true; }
            }
            return true;
    });

    /// <summary>Finishes the unlock the sign-in already paid for, without asking for the password
    /// again. Null means history is readable; anything else is the reason it is not, kept as the
    /// crypto module worded it rather than collapsed into one unexplained failure.</summary>
    private async Task<string?> UnlockAsync(Operation operation)
    {
        // A worker holding no sign-in material answers false immediately and sends nothing, so an
        // ordinary reload of a locked device costs one call, and never a second published device.
        try { return await JsAsync<bool>(operation, "passwordRestore") ? null : LockedMessage; }
        catch (OperationCanceledException) { throw; }
        catch (Exception failure)
        {
            Check(operation);
            return failure is JSException or InvalidOperationException && failure.Message.Split('\n')[0].Trim() is { Length: > 0 } reason
                ? reason : LockedMessage;
        }
    }

    public async Task<JsonElement[]> RecipientsAsync(UserSession user, Guid thread, bool allowPending = false, List<Guid>? audience = null)
    {
        var operation = Begin(user);
        // Call-control and edits retain their fresh-directory checks. Only message sends
        // have the server's current-roster precondition and safe 412 retry path.
        if (!allowPending || audience is not null)
        {
            return await LoadRecipientsAsync(operation, thread, allowPending, null, audience);
        }
        var key = (thread, allowPending);
        while (true)
        {
            Check(operation);
            RecipientRoster roster;
            lock (recipientsGate)
            {
                if (!recipients.TryGetValue(key, out roster!) ||
                    roster.Loading.IsCompleted && roster.ExpiresAt <= clock.GetUtcNow())
                {
                    if (recipients.Count >= 8 && !recipients.ContainsKey(key)) recipients.Remove(recipients.Keys.First());
                    roster = new RecipientRoster { Version = recipientVersion };
                    recipients[key] = roster;
                    roster.Loading = LoadRecipientsAsync(operation, thread, allowPending, roster);
                }
            }
            JsonElement[] directories;
            try { directories = await roster.Loading; }
            catch (RecipientRosterChangedException) { continue; }
            catch
            {
                lock (recipientsGate)
                    if (recipients.TryGetValue(key, out var current) && ReferenceEquals(current, roster)) recipients.Remove(key);
                throw;
            }
            Check(operation);
            lock (recipientsGate) { if (roster.Invalidated || roster.Version != recipientVersion) continue; }
            // Never expose the cached array itself or cache a caller's restricted audience.
            return directories.Where(x => audience is not { Count: > 0 } || audience.Contains(x.GetProperty("credentialId").GetGuid())).ToArray();
        }
    }

    private async Task<JsonElement[]> LoadRecipientsAsync(Operation operation, Guid thread, bool allowPending, RecipientRoster? roster, List<Guid>? audience = null)
    {
        var directories = await GetAsync<JsonElement[]>(operation, $"api/chat/conversations/{thread}/encryption?allowPending={allowPending.ToString().ToLowerInvariant()}");
        if (audience is { Count: > 0 }) directories = directories.Where(x => audience.Contains(x.GetProperty("credentialId").GetGuid())).ToArray();
        foreach (var directory in directories)
        {
            lock (recipientsGate) { if (roster is not null && (roster.Invalidated || roster.Version != recipientVersion)) throw new RecipientRosterChangedException(); }
            await JsVoidAsync(operation, "acceptDirectory", directory);
        }
        var status = await JsAsync<EncryptionStatus>(operation, "status");
        lock (recipientsGate)
        {
            if (roster is not null && (roster.Invalidated || roster.Version != recipientVersion)) throw new RecipientRosterChangedException();
            Status = status;
            if (roster is not null) roster.ExpiresAt = clock.GetUtcNow().Add(RecipientRosterLifetime);
        }
        return directories;
    }

    public static object Context(UserSession user, ChatMessage message, string kind = "message") => new
    {
        tenantId = user.TenantId.ToString(), threadId = message.ThreadId.ToString(), messageId = message.Id.ToString(),
        senderId = message.SenderId.ToString(), parentId = message.ParentId?.ToString(), isThreadReply = message.IsThreadReply, kind,
        expectedSenderDeviceId = message.EncryptionSenderDeviceId?.ToString(), expectedSenderDirectoryRevision = message.AcceptedSenderDirectoryRevision
    };

    public async Task<string> EncryptAsync(UserSession user, ChatMessage message, JsonElement[] recipients)
    {
        var operation = Begin(user);
        if (!Status.Approved) throw new InvalidOperationException("Unlock encrypted messages in Settings before sending.");
        return await JsAsync<string>(operation, "encrypt", Context(user, message),
            new { text = message.Text, attachments = message.Attachments }, recipients);
    }

    public async Task DecryptAsync(UserSession user, IEnumerable<ChatMessage> messages)
    {
        var operation = Begin(user);
        var encrypted = messages.Where(x => x.EncryptedEnvelope is not null).ToList();
        var directories = new Dictionary<(Guid, Guid?), JsonElement>();
        foreach (var group in encrypted.GroupBy(x => x.ThreadId))
        {
            foreach (var message in group)
            {
                try
                {
                    if (message.EncryptionPending)
                    {
                        Lock(message); message.Text = "Waiting for secure delivery"; continue;
                    }
                    var senderKey = (message.SenderId, message.EncryptionSenderDeviceId);
                    if (!directories.TryGetValue(senderKey, out var sender))
                        directories[senderKey] = sender = await GetAsync<JsonElement>(operation, SenderDirectoryPath(message.SenderId, message.EncryptionSenderDeviceId));
                    var payload = await JsAsync<EncryptedMessageContent>(operation, "decrypt",
                        Context(user, message), message.EncryptedEnvelope, sender);
                    message.Text = payload.Text; message.Attachments = payload.Attachments;
                    message.HasAttachments = payload.Attachments.Count > 0; message.EncryptionLocked = false;
                }
                catch (OperationCanceledException) { throw; }
                catch { Check(operation); Lock(message); }
            }
        }
    }

    public Task RestoreAsync(UserSession user, string key) => ChangeAsync(user, async operation =>
    {
        var directory = await GetAsync<JsonElement>(operation, "api/chat/encryption/directory");
        var local = await JsAsync<EncryptionStatus>(operation, "status");
        var changed = await JsAsync<bool>(operation, "observeOwnDirectory", directory);
        if (!changed && local.Enrolled && local.RootFingerprint is not null)
        {
            // A previous restore may have published its roster before this tab
            // lost the response. Accept its persisted fresh identity, not another.
            await JsVoidAsync(operation, "acceptDirectory", directory);
            Status = await JsAsync<EncryptionStatus>(operation, "status");
            if (Status.Approved) { if (Status.CanApproveDevices) await TryBackupAsync(operation); return true; }
        }
        var backup = await GetAsync<JsonElement>(operation, "api/chat/encryption/recovery");
        var restored = await JsAsync<JsonElement>(operation, "recovery", key,
            backup.GetProperty("archive").GetString(), directory);
        var confirmed = await PublishDirectoryAsync(operation, restored.GetProperty("directory"));
        await JsVoidAsync(operation, "acceptDirectory", confirmed);
        Status = await JsAsync<EncryptionStatus>(operation, "status");
        if (Status.Approved) Locked = null;
        await TryBackupAsync(operation);
        return true;
    });

    public Task ShowRecoveryAsync(UserSession user)
    {
        Begin(user);
        var revealRequest = ++revealGeneration;
        return ChangeAsync(user, async operation => { await SaveBackupAsync(operation, reveal: true, revealRequest); return true; });
    }
    private async Task TryBackupAsync(Operation operation)
    {
        try { await SaveBackupAsync(operation, reveal: false); }
        catch { Check(operation); BackupPending = true; }
    }
    private async Task SaveBackupAsync(Operation operation, bool reveal, long revealRequest = 0)
    {
        long revision = 0;
        string? previousArchive = null;
        try { var previous = await GetAsync<JsonElement>(operation, "api/chat/encryption/recovery");
            revision = previous.GetProperty("revision").GetInt64(); previousArchive = previous.GetProperty("archive").GetString(); }
        catch (ChatApiException ex) when (ex.Status == 404) { }
        var directory = await GetAsync<JsonElement>(operation, "api/chat/encryption/directory");
        await JsVoidAsync(operation, "acceptDirectory", directory);
        await JsVoidAsync(operation, "mergeRecovery", previousArchive, directory);
        var recovery = await JsAsync<JsonElement>(operation, "exportRecovery");
        await PostAsync<object>(operation, "api/chat/encryption/recovery", new { expectedRevision = revision,
            archive = recovery.GetProperty("recoveryArchive").GetString(), rootPublicKey = directory.GetProperty("rootPublicKey").GetString() });
        if (reveal && revealRequest == revealGeneration) RecoveryKey = recovery.GetProperty("recoveryKey").GetString();
        backedUpRevision = Status.DirectoryRevision; BackupPending = false;
    }
    public Task<string> ProposeAsync(UserSession user) => ChangeAsync(user, async operation =>
        (await JsAsync<JsonElement>(operation, "proposeDevice")).GetRawText());
    public Task<string> ApproveAsync(UserSession user, string proposal) => ChangeAsync(user, async operation =>
    {
        var directory = await GetAsync<JsonElement>(operation, "api/chat/encryption/directory");
        var approved = await JsAsync<JsonElement>(operation, "approveDevice", JsonSerializer.Deserialize<JsonElement>(proposal), directory);
        var confirmed = approved.TryGetProperty("alreadyPublished", out var published) && published.GetBoolean()
            ? approved.GetProperty("directory") : await PublishDirectoryAsync(operation, approved.GetProperty("directory"));
        await JsVoidAsync(operation, "acceptDirectory", confirmed);
        Status = await JsAsync<EncryptionStatus>(operation, "status");
        await TryBackupAsync(operation);
        return approved.GetProperty("approval").GetRawText();
    });
    public Task AcceptApprovalAsync(UserSession user, string approval) => ChangeAsync(user, async operation =>
    {
        var directory = await GetAsync<JsonElement>(operation, "api/chat/encryption/directory");
        await JsVoidAsync(operation, "importApproval", JsonSerializer.Deserialize<JsonElement>(approval), directory);
        Status = await JsAsync<EncryptionStatus>(operation, "status");
        if (Status.Approved) Locked = null;
        if (Status.CanApproveDevices) await TryBackupAsync(operation);
        return true;
    });
    public Task<List<DeviceInfo>> DevicesAsync(UserSession user) => ChangeAsync(user, async operation =>
    {
        var directory = await GetAsync<JsonElement>(operation, "api/chat/encryption/directory");
        await JsVoidAsync(operation, "acceptDirectory", directory);
        Status = await JsAsync<EncryptionStatus>(operation, "status");
        return directory.GetProperty("devices").EnumerateArray().Select(d => new DeviceInfo(d.GetProperty("deviceId").GetGuid(),
            d.GetProperty("deviceId").GetGuid() == Status.DeviceId,
            d.TryGetProperty("revocation", out var revoked) && revoked.ValueKind != JsonValueKind.Null)).ToList();
    });
    public Task RevokeAsync(UserSession user, Guid deviceId) => ChangeAsync(user, async operation =>
    {
        if (!Status.CanApproveDevices || deviceId == Status.DeviceId) throw new InvalidOperationException("Use the owner device to remove another device.");
        var directory = await GetAsync<JsonElement>(operation, "api/chat/encryption/directory");
        var target = directory.GetProperty("devices").EnumerateArray().Single(d => d.GetProperty("deviceId").GetGuid() == deviceId);
        if (target.TryGetProperty("revocation", out var revoked) && revoked.ValueKind != JsonValueKind.Null)
        { await JsVoidAsync(operation, "acceptDirectory", directory); return true; }
        var update = await JsAsync<JsonElement>(operation, "revokeDevice", deviceId.ToString(), directory);
        var confirmed = await PublishDirectoryAsync(operation, update.GetProperty("directory"));
        await JsVoidAsync(operation, "acceptDirectory", confirmed);
        Status = await JsAsync<EncryptionStatus>(operation, "status");
        await TryBackupAsync(operation);
        return true;
    });
    public Task<FingerprintInfo> FingerprintAsync(UserSession user, Guid credentialId) => ChangeAsync(user, async operation =>
    {
        var directory = await GetAsync<JsonElement>(operation, $"api/chat/encryption/people/{credentialId}");
        var pin = await JsAsync<JsonElement>(operation, "inspectDirectory", directory);
        return new FingerprintInfo(credentialId, pin.GetProperty("fingerprint").GetString()!, pin.GetProperty("verified").GetBoolean(), pin.GetProperty("changed").GetBoolean());
    });
    public Task VerifyFingerprintAsync(UserSession user, Guid credentialId, string fingerprint) => ChangeAsync(user, async operation =>
    {
        // Recheck the signed server roster before marking the compared pin verified.
        var directory = await GetAsync<JsonElement>(operation, $"api/chat/encryption/people/{credentialId}");
        await JsAsync<bool>(operation, "verifyDirectory", directory, fingerprint);
        return true;
    });
    private async Task<JsonElement> PublishDirectoryAsync(Operation operation, JsonElement directory)
    {
        try { return await PostAsync<JsonElement>(operation, "api/chat/encryption/directory", new
        {
            expectedRevision = directory.GetProperty("revision").GetInt64() - 1,
            rootPublicKey = directory.GetProperty("rootPublicKey").GetString(),
            roster = directory.GetProperty("roster").GetString(), devices = directory.GetProperty("devices")
        }); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException || ex is ChatApiException { Status: 409 or >= 500 })
        {
            Check(operation);
            // A failed/lost POST response does not mean the atomic write failed.
            var confirmed = await GetAsync<JsonElement>(operation, "api/chat/encryption/directory");
            if (confirmed.GetProperty("rootPublicKey").GetString() == directory.GetProperty("rootPublicKey").GetString()
                && confirmed.GetProperty("roster").GetString() == directory.GetProperty("roster").GetString()) return confirmed;
            throw;
        }
    }
    public sealed record DeviceInfo(Guid Id, bool IsLocal, bool Revoked);
    public sealed record FingerprintInfo(Guid CredentialId, string Fingerprint, bool Verified, bool Changed = false);
    public static string SenderDirectoryPath(Guid sender, Guid? device) => $"api/chat/encryption/people/{sender}" + (device is null ? "" : $"?senderDeviceId={device}");

    public Task ResetIdentityAsync(UserSession user, string password) => ChangeAsync(user, async operation =>
    {
        if (await JsAsync<bool>(operation, "passwordReset", password))
        {
            HideRecovery(); backedUpRevision = -1;
            Status = await JsAsync<EncryptionStatus>(operation, "status");
            return true;
        }
        var current = await GetAsync<JsonElement>(operation, "api/chat/encryption/directory");
        if (!await JsAsync<bool>(operation, "confirmReset", current))
        {
            var pending = await JsAsync<JsonElement>(operation, "prepareReset", current);
            var directory = pending.GetProperty("directory");
            try
            {
                current = await PostAsync<JsonElement>(operation, "api/chat/encryption/reset", new { password,
                    directory = new { expectedRevision = directory.GetProperty("revision").GetInt64() - 1,
                        rootPublicKey = directory.GetProperty("rootPublicKey").GetString(), roster = directory.GetProperty("roster").GetString(), devices = directory.GetProperty("devices") },
                    recoveryArchive = pending.GetProperty("recoveryArchive").GetString() });
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException || ex is ChatApiException { Status: 409 or >= 500 })
            {
                current = await GetAsync<JsonElement>(operation, "api/chat/encryption/directory");
                if (current.GetProperty("rootPublicKey").GetString() != directory.GetProperty("rootPublicKey").GetString()) throw;
            }
            if (!await JsAsync<bool>(operation, "confirmReset", current)) throw new InvalidOperationException("Reset was not confirmed.");
        }
        HideRecovery(); backedUpRevision = -1;
        Status = await JsAsync<EncryptionStatus>(operation, "status");
        return true;
    });
    public Task AcknowledgeResetAsync(UserSession user) => ChangeAsync(user, async operation =>
    { await JsVoidAsync(operation, "acknowledgeReset"); Status = await JsAsync<EncryptionStatus>(operation, "status"); return true; });

    private static void Lock(ChatMessage message)
    {
        message.Text = "Encrypted message · unlock or verify this device in Settings";
        message.Attachments = []; message.HasAttachments = false; message.EncryptionLocked = true;
    }
    public sealed class EncryptionStatus
    {
        public bool Enrolled { get; set; }
        public bool Approved { get; set; }
        public Guid? DeviceId { get; set; }
        public string? RootFingerprint { get; set; }
        public long DirectoryRevision { get; set; }
        public bool CanApproveDevices { get; set; }
        public bool ResetHistoryPending { get; set; }
    }
    internal sealed record EncryptedMessageContent(string Text, List<ChatAttachment> Attachments);
}
