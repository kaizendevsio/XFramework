using Microsoft.AspNetCore.SignalR.Client;
using XFramework.Domain.Shared.DataContext;
using XFramework.Integration.Abstractions;

namespace XFramework.Integration.DataContext;

public class RemoteDataContext : IDataContext
{
    private readonly ISignalRService _signalRService;
    private readonly List<ChangeEntry> _pendingChanges = [];

    public RemoteDataContext(ISignalRService signalRService)
    {
        _signalRService = signalRService;
    }

    public IRemoteQuery<T> Query<T>() where T : class
    {
        var connection = _signalRService.Connection
            ?? throw new InvalidOperationException("StreamFlow connection is not established.");
        return new RemoteQuery<T>(connection);
    }

    public void Add<T>(T entity) where T : class
    {
        _pendingChanges.Add(new ChangeEntry
        {
            EntityTypeName = typeof(T).Name,
            Operation = ChangeOperation.Add,
            SerializedEntity = MemoryPack.MemoryPackSerializer.Serialize(entity)
        });
    }

    public void Update<T>(T entity) where T : class
    {
        _pendingChanges.Add(new ChangeEntry
        {
            EntityTypeName = typeof(T).Name,
            Operation = ChangeOperation.Update,
            SerializedEntity = MemoryPack.MemoryPackSerializer.Serialize(entity)
        });
    }

    public void Remove<T>(T entity) where T : class
    {
        _pendingChanges.Add(new ChangeEntry
        {
            EntityTypeName = typeof(T).Name,
            Operation = ChangeOperation.Remove,
            SerializedEntity = MemoryPack.MemoryPackSerializer.Serialize(entity)
        });
    }

    public async Task<DataContextResult> SaveChangesAsync(CancellationToken ct = default)
    {
        if (_pendingChanges.Count == 0)
            return DataContextResult.Success("No changes to save.");

        var connection = _signalRService.Connection
            ?? throw new InvalidOperationException("StreamFlow connection is not established.");

        var request = new SaveChangesRequest { Changes = [.._pendingChanges] };
        var requestBytes = MemoryPack.MemoryPackSerializer.Serialize(request);
        var resultBytes = await connection.InvokeAsync<byte[]>("ExecuteChanges", requestBytes, ct);

        var result = MemoryPack.MemoryPackSerializer.Deserialize<DataContextResult>(resultBytes);

        if (result?.IsSuccess == true)
        {
            _pendingChanges.Clear();
        }

        return result ?? DataContextResult.Failure("Failed to deserialize response.");
    }
}
