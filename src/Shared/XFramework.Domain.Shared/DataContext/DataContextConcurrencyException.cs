namespace XFramework.Domain.Shared.DataContext;

public class DataContextConcurrencyException : Exception
{
    public string EntityTypeName { get; init; } = string.Empty;
    public byte[] EntityId { get; init; } = [];
    public Dictionary<string, byte[]> CurrentDbValues { get; init; } = new();
    public Dictionary<string, byte[]> ClientValues { get; init; } = new();

    public DataContextConcurrencyException(string message) : base(message) { }
    public DataContextConcurrencyException(string message, Exception inner) : base(message, inner) { }
}
