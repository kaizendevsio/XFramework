using System.Net;
using Bolt.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using XFramework.Domain.Shared.DataContext;
using XFramework.Integration.Abstractions;

namespace XFramework.Core.DataContext;

public class DataContextBoltHandler : IBoltHandler
{
    public void Register(BoltClient client, ILogger logger, IServiceScopeFactory scopeFactory)
    {
        client.RegisterHandler("__db_query__", async (payload, requestId) =>
        {
            using var scope = scopeFactory.CreateScope();
            var queryService = scope.ServiceProvider.GetRequiredService<IQueryExecutionService>();
            try
            {
                var result = await queryService.ExecuteAsync(payload.ToArray());
                return (HttpStatusCode.OK, (ReadOnlyMemory<byte>)result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "__db_query__ failed (requestId={RequestId})", requestId);
                var error = MemoryPack.MemoryPackSerializer.Serialize(DataContextResult.Failure(ex.Message));
                return (HttpStatusCode.InternalServerError, (ReadOnlyMemory<byte>)error);
            }
        });

        client.RegisterHandler("__db_changes__", async (payload, requestId) =>
        {
            using var scope = scopeFactory.CreateScope();
            var queryService = scope.ServiceProvider.GetRequiredService<IQueryExecutionService>();
            try
            {
                var result = await queryService.ExecuteChangesAsync(payload.ToArray());
                return (HttpStatusCode.OK, (ReadOnlyMemory<byte>)result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "__db_changes__ failed (requestId={RequestId})", requestId);
                var error = MemoryPack.MemoryPackSerializer.Serialize(DataContextResult.Failure(ex.Message));
                return (HttpStatusCode.InternalServerError, (ReadOnlyMemory<byte>)error);
            }
        });

        client.RegisterStreamHandler("__db_query_stream__", async stream =>
        {
            using var scope = scopeFactory.CreateScope();
            var queryService = scope.ServiceProvider.GetRequiredService<IQueryExecutionService>();

            var (hasData, payload) = await stream.ReadAsync();
            if (!hasData)
            {
                await stream.CloseAsync(HttpStatusCode.BadRequest);
                return;
            }

            try
            {
                var descriptorBytes = payload.ToArray();
                var descriptor = MemoryPack.MemoryPackSerializer.Deserialize<QueryDescriptor>(
                    (ReadOnlySpan<byte>)descriptorBytes);
                var chunkSize = descriptor?.ChunkSize ?? 100;

                var chunk = new List<byte[]>(chunkSize);
                await foreach (var entityBytes in queryService.ExecuteStreamAsync(descriptorBytes))
                {
                    chunk.Add(entityBytes);
                    if (chunk.Count >= chunkSize)
                    {
                        await stream.SendAsync((ReadOnlyMemory<byte>)MemoryPack.MemoryPackSerializer.Serialize(chunk));
                        chunk = new List<byte[]>(chunkSize);
                    }
                }

                if (chunk.Count > 0)
                    await stream.SendAsync((ReadOnlyMemory<byte>)MemoryPack.MemoryPackSerializer.Serialize(chunk));

                await stream.CloseAsync(HttpStatusCode.OK);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "__db_query_stream__ failed");
                await stream.CloseAsync(HttpStatusCode.InternalServerError);
            }
        });

        logger.LogInformation("Registered DataContext Bolt handlers (__db_query__, __db_changes__, __db_query_stream__)");
    }
}
