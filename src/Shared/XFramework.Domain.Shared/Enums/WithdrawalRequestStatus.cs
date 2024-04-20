namespace XFramework.Domain.Shared.Enums;

public enum WithdrawalRequestStatus : byte
{
    None = 0,
    Pending = 1,
    Completed = 2,
    Rejected = 10
}