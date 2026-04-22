namespace XFramework.Domain.Shared.DataContext;

public interface IDataContextServiceWrapper
{
    Task<byte[]> ExecuteQueryAsync(byte[] queryDescriptorBytes, CancellationToken ct = default);
    Task<byte[]> ExecuteChangesAsync(byte[] saveChangesRequestBytes, CancellationToken ct = default);
    IAsyncEnumerable<byte[]> ExecuteQueryStreamAsync(byte[] queryDescriptorBytes, CancellationToken ct = default);
}
