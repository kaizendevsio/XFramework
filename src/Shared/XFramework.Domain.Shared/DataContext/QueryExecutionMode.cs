namespace XFramework.Domain.Shared.DataContext;

public enum QueryExecutionMode : byte
{
    ToList = 0,
    FirstOrDefault = 1,
    Count = 2,
    Stream = 3,
    SingleOrDefault = 4,
    Any = 5,
    AnyWithPredicate = 6,
    All = 7,
    Min = 8,
    Max = 9,
    MinBy = 10,
    MaxBy = 11,
    Sum = 12,
    Average = 13,
    GroupBy = 14,
}
