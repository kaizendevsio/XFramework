using Microsoft.JSInterop;

namespace Yap.Client.Services;

public sealed partial class ChatState
{
    public async Task ResetEncryptionAsync(string password)
    {
        await sync.WaitAsync(lifetime.Token);
        try
        {
            if (User is null || NeedsLogin || !Online) throw new InvalidOperationException("Sign in and connect before resetting encryption.");
            await localChanges.WaitAsync(lifetime.Token);
            try { await Encryption.ResetIdentityAsync(User, password); }
            finally { localChanges.Release(); }
            await FinishEncryptionResetAsync();
        }
        finally { sync.Release(); Notify(); }
        await SynchronizeAsync();
    }

    private async Task FinishEncryptionResetAsync()
    {
        if (User is null || !Encryption.Status.ResetHistoryPending) return;
        await localChanges.WaitAsync(lifetime.Token);
        try
        {
            // The identity and backup are already committed. Cleanup is retryable after a crash.
            var files = await store.EncryptionResetFilesAsync(Scope);
            await js.InvokeVoidAsync("yap.device.clearAccountFiles", Scope, files);
            await store.ClearEncryptionHistoryAsync(Scope);
            Selected = null; Conversations = []; PendingCount = 0;
            await Encryption.AcknowledgeResetAsync(User);
        }
        finally { localChanges.Release(); }
    }
}
