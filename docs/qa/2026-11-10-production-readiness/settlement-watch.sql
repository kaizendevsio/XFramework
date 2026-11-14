-- Read-only monitor input. Page on nonzero counts; treat query errors as monitor failures, not zero.
-- A five-minute grace interval avoids paging during normal in-flight work.
WITH threshold AS (SELECT current_timestamp - interval '5 minutes' AS cutoff)
SELECT 'stalled_paid_or_pending_sales' AS signal, count(*) AS outstanding
FROM "POS"."PosSale", threshold
WHERE NOT "IsDeleted" AND "Status" IN (3, 4, 7) -- PaymentPending, PaymentCaptured, InventoryFulfillmentFailed
  AND coalesce("ModifiedAt", "CreatedAt") < cutoff
UNION ALL
SELECT 'stalled_returns', count(*)
FROM "POS"."PosReturn", threshold
WHERE NOT "IsDeleted" AND "Status" IN (0, 1, 4, 5, 6) -- Pending, InventoryPosted, Failed, InventoryPostFailed, RefundFailed
  AND coalesce("ModifiedAt", "CreatedAt") < cutoff
UNION ALL
SELECT 'wallet_reconciliation_exceptions', count(*)
FROM "Wallet"."WalletReconciliationItem"
WHERE NOT "IsDeleted" AND "Status" IN (2, 4, 5) AND "MarkedReconciledAt" IS NULL;
