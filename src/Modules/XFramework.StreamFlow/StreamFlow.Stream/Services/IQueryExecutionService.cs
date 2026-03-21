using XFramework.Domain.Shared.DataContext;

namespace StreamFlow.Stream.Services;

public interface IQueryExecutionService
{
    void RegisterEntity<T>(string name) where T : class;
    void RegisterEntity(Type entityType, string name);
    Task<byte[]> ExecuteAsync(byte[] queryDescriptorBytes, CancellationToken ct = default);
    IAsyncEnumerable<byte[]> ExecuteStreamAsync(byte[] queryDescriptorBytes, CancellationToken ct = default);
    Task<byte[]> ExecuteChangesAsync(byte[] saveChangesRequestBytes, CancellationToken ct = default);
}
