using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.JSInterop;
using Yap.Contracts;

namespace Yap.Client.Services;

/// <summary>Moves public directories and ciphertext only. Private keys stay in the browser crypto module.</summary>
public sealed class ChatEncryption(ChatApi api, IJSRuntime js)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private string? activeScope;
    private long generation;
    private long revealGeneration;
    private long backedUpRevision = -1;
    public bool BackupPending { get; private set; }
    public EncryptionStatus Status { get; private set; } = new();
    public string? RecoveryKey { get; private set; }
    public Guid? LocalDeviceId => Status.DeviceId;
    public void Reset() { generation++; activeScope = null; HideRecovery(); Status = new(); backedUpRevision = -1; BackupPending = false; }
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
    { await gate.WaitAsync(); try { return await action(Begin(user)); } finally { gate.Release(); } }

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
            JsonElement directory;
            try { directory = await GetAsync<JsonElement>(operation, "api/chat/encryption/directory"); }
            catch (ChatApiException ex) when (ex.Status == 404)
            {
                var enrollment = await JsAsync<JsonElement>(operation, "initialize");
                directory = await PublishDirectoryAsync(operation, enrollment.GetProperty("directory"));
                await JsVoidAsync(operation, "acceptDirectory", directory);
            }
            var local = await JsAsync<EncryptionStatus>(operation, "status");
            if (!local.Enrolled)
            {
                await JsAsync<JsonElement>(operation, "proposeDevice");
                local = await JsAsync<EncryptionStatus>(operation, "status");
            }
            if (local.RootFingerprint is null) { Status = local; return true; }
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

    public async Task<JsonElement[]> RecipientsAsync(UserSession user, Guid thread, bool allowPending = false, List<Guid>? audience = null)
    {
        var operation = Begin(user);
        var directories = await GetAsync<JsonElement[]>(operation, $"api/chat/conversations/{thread}/encryption?allowPending={allowPending.ToString().ToLowerInvariant()}");
        if (audience is { Count: > 0 }) directories = directories.Where(x => audience.Contains(x.GetProperty("credentialId").GetGuid())).ToArray();
        foreach (var directory in directories)
            await JsVoidAsync(operation, "acceptDirectory", directory);
        Status = await JsAsync<EncryptionStatus>(operation, "status");
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
        foreach (var group in encrypted.GroupBy(x => x.ThreadId))
        {
            var directories = new Dictionary<Guid, JsonElement>();
            foreach (var message in group)
            {
                try
                {
                    if (message.EncryptionPending)
                    {
                        Lock(message); message.Text = "Waiting for secure delivery"; continue;
                    }
                    if (!directories.TryGetValue(message.SenderId, out var sender))
                        directories[message.SenderId] = sender = await GetAsync<JsonElement>(operation, $"api/chat/encryption/people/{message.SenderId}");
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
        if (local.Enrolled && local.RootFingerprint is not null)
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
        var recovery = await JsAsync<JsonElement>(operation, "exportRecovery");
        long revision = 0;
        try { revision = (await GetAsync<JsonElement>(operation, "api/chat/encryption/recovery")).GetProperty("revision").GetInt64(); }
        catch (ChatApiException ex) when (ex.Status == 404) { }
        await PostAsync<object>(operation, "api/chat/encryption/recovery", new { expectedRevision = revision,
            archive = recovery.GetProperty("recoveryArchive").GetString() });
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
        var pin = await JsAsync<JsonElement>(operation, "acceptDirectory", directory);
        return new FingerprintInfo(credentialId, pin.GetProperty("fingerprint").GetString()!, pin.GetProperty("verified").GetBoolean());
    });
    public Task VerifyFingerprintAsync(UserSession user, Guid credentialId, string fingerprint) => ChangeAsync(user, async operation =>
    {
        // Recheck the signed server roster before marking the compared pin verified.
        var directory = await GetAsync<JsonElement>(operation, $"api/chat/encryption/people/{credentialId}");
        await JsVoidAsync(operation, "acceptDirectory", directory);
        await JsAsync<bool>(operation, "verifyFingerprint", credentialId.ToString(), fingerprint);
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
    public sealed record FingerprintInfo(Guid CredentialId, string Fingerprint, bool Verified);

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
    }
    private sealed record EncryptedMessageContent(string Text, List<ChatAttachment> Attachments);
}
