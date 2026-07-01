namespace POS.Domain.Shared.Enums;

public enum PosSaleStatus
{
    Draft = 0,
    ReservingInventory = 1,
    InventoryReserved = 2,
    PaymentPending = 3,
    PaymentCaptured = 4,
    Completed = 5,
    PaymentFailed = 6,
    InventoryFulfillmentFailed = 7,
    Cancelled = 8,
    InventoryReservationFailed = 9
}

public enum PosPaymentMethod
{
    CashDrawer = 0,
    WalletTransfer = 1
}

public enum PosPaymentStatus
{
    Pending = 0,
    Captured = 1,
    Failed = 2,
    Refunded = 3
}

public enum PosCartStatus
{
    Open = 0,
    Suspended = 1,
    Converted = 2,
    Cancelled = 3,
    Expired = 4
}

public enum PosReturnStatus
{
    Pending = 0,
    InventoryPosted = 1,
    Refunded = 2,
    Completed = 3,
    Failed = 4
}
