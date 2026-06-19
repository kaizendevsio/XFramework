namespace Wallets.Domain.Shared.Enums;

public enum WalletWebhookProcessingStatus
{
    Received = 0,
    Processing = 1,
    Processed = 2,
    Duplicate = 3,
    Failed = 4,
    Rejected = 5
}
