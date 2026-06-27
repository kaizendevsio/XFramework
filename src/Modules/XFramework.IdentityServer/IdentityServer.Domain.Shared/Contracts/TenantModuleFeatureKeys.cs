namespace IdentityServer.Domain.Shared.Contracts;

public static class TenantModuleFeatureKeys
{
    public const string Wallets = "wallets";
    public const string WalletsTransfers = "wallets.transfers";
    public const string WalletsDeposits = "wallets.deposits";
    public const string WalletsWithdrawals = "wallets.withdrawals";
    public const string WalletsBatch = "wallets.batch";
    public const string WalletsReconciliation = "wallets.reconciliation";
    public const string WalletsPolicy = "wallets.policy";
    public const string WalletsWebhooks = "wallets.webhooks";
    public const string WalletsReporting = "wallets.reporting";
    public const string Inventario = "inventario";
    public const string InventarioCatalog = "inventario.catalog";
    public const string InventarioVariations = "inventario.variations";
    public const string InventarioTransactions = "inventario.transactions";
    public const string InventarioLowStockAlerts = "inventario.low_stock_alerts";
    public const string InventarioWarehousing = "inventario.warehousing";
    public const string InventarioStockBalances = "inventario.stock_balances";
    public const string InventarioMovements = "inventario.movements";
    public const string InventarioReservations = "inventario.reservations";
    public const string InventarioFulfillment = "inventario.fulfillment";
    public const string InventarioPurchasing = "inventario.purchasing";
    public const string InventarioTraceability = "inventario.traceability";
    public const string InventarioPlanning = "inventario.planning";
    public const string InventarioReporting = "inventario.reporting";
    public const string InventarioNegativeStock = "inventario.negative_stock";
    public const string Messaging = "messaging";
    public const string MessagingChat = "messaging.chat";
    public const string MessagingAudioVideo = "messaging.audio_video";
    public const string Community = "community";
    public const string Payments = "payments";
    public const string Notifications = "notifications";
    public const string Attendance = "attendance";
    public const string Storage = "storage";

    public const string CatalogSubFeature = "catalog";
    public const string TransfersSubFeature = "transfers";
    public const string DepositsSubFeature = "deposits";
    public const string WithdrawalsSubFeature = "withdrawals";
    public const string BatchSubFeature = "batch";
    public const string ReconciliationSubFeature = "reconciliation";
    public const string PolicySubFeature = "policy";
    public const string WebhooksSubFeature = "webhooks";
    public const string ReportingSubFeature = "reporting";
    public const string VariationsSubFeature = "variations";
    public const string TransactionsSubFeature = "transactions";
    public const string LowStockAlertsSubFeature = "low_stock_alerts";
    public const string WarehousingSubFeature = "warehousing";
    public const string StockBalancesSubFeature = "stock_balances";
    public const string MovementsSubFeature = "movements";
    public const string ReservationsSubFeature = "reservations";
    public const string FulfillmentSubFeature = "fulfillment";
    public const string PurchasingSubFeature = "purchasing";
    public const string TraceabilitySubFeature = "traceability";
    public const string PlanningSubFeature = "planning";
    public const string InventarioReportingSubFeature = "reporting";
    public const string NegativeStockSubFeature = "negative_stock";
    public const string ChatSubFeature = "chat";
    public const string AudioVideoSubFeature = "audio_video";

    public static IReadOnlyList<TenantModuleFeatureDefinition> All { get; } =
    [
        new(Wallets, string.Empty, "Wallets", "Wallet accounts, balances, transfers, deposits, and withdrawals.", "wallet"),
        new(Wallets, TransfersSubFeature, "Wallet Transfers", "Wallet transfer and conversion operations.", "arrow-left-right"),
        new(Wallets, DepositsSubFeature, "Wallet Deposits", "Deposit requests, approvals, provider callbacks, and settlement.", "circle-plus"),
        new(Wallets, WithdrawalsSubFeature, "Wallet Withdrawals", "Withdrawal requests, holds, approvals, payout settlement, and failures.", "circle-minus"),
        new(Wallets, BatchSubFeature, "Wallet Batch", "Batch wallet balance and transfer operations.", "rows-3"),
        new(Wallets, ReconciliationSubFeature, "Wallet Reconciliation", "Ledger, balance, transaction, and provider reconciliation.", "scale"),
        new(Wallets, PolicySubFeature, "Wallet Policy", "Wallet policy, risk, fee, and approval rules.", "shield-check"),
        new(Wallets, WebhooksSubFeature, "Wallet Webhooks", "Payment provider webhook ingestion and outbox delivery.", "webhook"),
        new(Wallets, ReportingSubFeature, "Wallet Reporting", "Statements, operation history, settlement, and failure reports.", "bar-chart-3"),
        new(Inventario, string.Empty, "Inventario", "Product catalog and inventory operations.", "boxes"),
        new(Inventario, CatalogSubFeature, "Catalog", "Products, categories, SKUs, and catalog attributes.", "package"),
        new(Inventario, VariationsSubFeature, "Variations", "Product options, variants, and variation-specific stock.", "git-branch"),
        new(Inventario, TransactionsSubFeature, "Transactions", "Inventory receipts, adjustments, transfers, and issue records.", "arrow-left-right"),
        new(Inventario, LowStockAlertsSubFeature, "Low Stock Alerts", "Low stock thresholds, alert review, and replenishment signals.", "bell"),
        new(Inventario, WarehousingSubFeature, "Warehousing", "Warehouse, location, bin, and storage-area workflows.", "warehouse", false),
        new(Inventario, StockBalancesSubFeature, "Stock Balances", "Stock-on-hand, available, allocated, and reserved balance views.", "scale", false),
        new(Inventario, MovementsSubFeature, "Movements", "Physical stock movement tracking between locations and states.", "move", false),
        new(Inventario, ReservationsSubFeature, "Reservations", "Stock reservations, holds, and allocation commitments.", "bookmark-check", false),
        new(Inventario, FulfillmentSubFeature, "Fulfillment", "Pick, pack, ship, and order fulfillment operations.", "truck", false),
        new(Inventario, PurchasingSubFeature, "Purchasing", "Purchase orders, receiving, and supplier replenishment workflows.", "shopping-cart", false),
        new(Inventario, TraceabilitySubFeature, "Traceability", "Lot, serial, batch, and inventory lineage tracking.", "scan-line", false),
        new(Inventario, PlanningSubFeature, "Planning", "Demand planning, reorder planning, and replenishment forecasting.", "calendar-clock", false),
        new(Inventario, InventarioReportingSubFeature, "Reporting", "Inventory analytics, valuation, audit, and operational reports.", "bar-chart-3", false),
        new(Inventario, NegativeStockSubFeature, "Negative Stock", "Controls for allowing or blocking negative stock positions.", "minus-circle", false),
        new(Messaging, string.Empty, "Messaging", "Tenant messaging settings, administration, moderation, and chat platform controls.", "messages-square"),
        new(Messaging, ChatSubFeature, "Messaging Chat", "Threads, direct messages, reactions, and attachments.", "message-circle"),
        new(Messaging, AudioVideoSubFeature, "Messaging Audio/Video", "Audio and video communication features.", "video"),
        new(Community, string.Empty, "Community", "Community identities, content, feed, and connections.", "users"),
        new(Payments, string.Empty, "Payments", "Payment gateway and cash-in/cash-out capabilities.", "credit-card"),
        new(Notifications, string.Empty, "Notifications", "Tenant notifications and read-state workflows.", "bell"),
        new(Attendance, string.Empty, "Attendance", "Attendance contexts, sessions, participants, time events, and reports.", "calendar-check"),
        new(Storage, string.Empty, "Storage", "Tenant file metadata, resumable uploads, signed URLs, and retention cleanup.", "hard-drive")
    ];

    public static (string ModuleKey, string SubFeatureKey) Normalize(string moduleKey, string? subFeatureKey = null)
    {
        var normalizedModuleKey = NormalizePart(moduleKey);
        var normalizedSubFeatureKey = NormalizePart(subFeatureKey);

        if (string.IsNullOrWhiteSpace(normalizedSubFeatureKey))
        {
            var separatorIndex = normalizedModuleKey.IndexOf('.', StringComparison.Ordinal);
            if (separatorIndex > 0 && separatorIndex < normalizedModuleKey.Length - 1)
            {
                normalizedSubFeatureKey = normalizedModuleKey[(separatorIndex + 1)..];
                normalizedModuleKey = normalizedModuleKey[..separatorIndex];
            }
        }

        return (normalizedModuleKey, normalizedSubFeatureKey);
    }

    public static string Combine(string moduleKey, string? subFeatureKey = null)
    {
        var (normalizedModuleKey, normalizedSubFeatureKey) = Normalize(moduleKey, subFeatureKey);
        return string.IsNullOrWhiteSpace(normalizedSubFeatureKey)
            ? normalizedModuleKey
            : $"{normalizedModuleKey}.{normalizedSubFeatureKey}";
    }

    public static TenantModuleFeatureDefinition? Find(string moduleKey, string? subFeatureKey = null)
    {
        var key = Combine(moduleKey, subFeatureKey);
        return All.FirstOrDefault(definition =>
            string.Equals(definition.Key, key, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizePart(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant();
}
