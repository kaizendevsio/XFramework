namespace HealthEssentials.Domain.Generics.Enums;

public enum TransactionRecordType
{
    NotSpecified = 0,
    Current = 1,
    Scheduled = 2,
    Completed = 3,
    Cancelled = 4,
    Rejected = 5,
    Expired = 6,
    Deleted = 7,
    Reversed = 8,
    OnGoing = 9,
}