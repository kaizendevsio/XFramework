namespace XFramework.Domain.Shared.DataContext;

[MemoryPackable]
public partial class DataContextResult
{
    [MemoryPackOrder(0)] public bool IsSuccess { get; set; }
    [MemoryPackOrder(1)] public string? Message { get; set; }
    [MemoryPackOrder(2)] public int StatusCode { get; set; }

    public static DataContextResult Success(string? message = null) => new()
    {
        IsSuccess = true,
        StatusCode = 200,
        Message = message
    };

    public static DataContextResult Failure(string message, int statusCode = 400) => new()
    {
        IsSuccess = false,
        Message = message,
        StatusCode = statusCode
    };
}
