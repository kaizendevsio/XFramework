using System.Net;
using Bolt.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using XFramework.Domain.Shared.DataContext;
using XFramework.Integration.Abstractions;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Security;

namespace XFramework.Core.DataContext;

public class DataContextBoltHandler : IBoltHandler
{
    public void Register(BoltClient client, ILogger logger, IServiceScopeFactory scopeFactory)
    {
        client.RegisterHandler("__db_query__", async (payload, context, ct) =>
        {
            using var scope = scopeFactory.CreateScope();
            var queryService = scope.ServiceProvider.GetRequiredService<IQueryExecutionService>();
            try
            {
                var descriptor = MemoryPack.MemoryPackSerializer.Deserialize<QueryDescriptor>(payload.Span);
                var authorization = await AuthorizeAsync(
                    scope.ServiceProvider,
                    descriptor?.Metadata,
                    context,
                    descriptor?.IgnoreQueryFilters == true
                        ? [XFrameworkServiceScopes.DataContextQuery, XFrameworkServiceScopes.DataContextQueryAllTenants]
                        : [XFrameworkServiceScopes.DataContextQuery],
                    ct);
                if (!authorization.IsSuccess)
                    return ((HttpStatusCode)authorization.StatusCode, SerializeFailure(authorization.Error));

                var result = await queryService.ExecuteAsync(payload.ToArray(), ct);
                return (HttpStatusCode.OK, (ReadOnlyMemory<byte>)result);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "__db_query__ failed (requestId={RequestId})", context.RequestId);
                var error = MemoryPack.MemoryPackSerializer.Serialize(
                    DataContextResult.Failure("Remote DataContext query failed."));
                return (HttpStatusCode.InternalServerError, (ReadOnlyMemory<byte>)error);
            }
        });

        client.RegisterHandler("__db_changes__", async (payload, context, ct) =>
        {
            using var scope = scopeFactory.CreateScope();
            var queryService = scope.ServiceProvider.GetRequiredService<IQueryExecutionService>();
            try
            {
                var request = MemoryPack.MemoryPackSerializer.Deserialize<SaveChangesRequest>(payload.Span);
                var authorization = await AuthorizeAsync(
                    scope.ServiceProvider,
                    request?.Metadata,
                    context,
                    [XFrameworkServiceScopes.DataContextMutate],
                    ct);
                if (!authorization.IsSuccess)
                    return ((HttpStatusCode)authorization.StatusCode, SerializeFailure(authorization.Error));

                var result = await queryService.ExecuteChangesAsync(payload.ToArray(), ct);
                return (HttpStatusCode.OK, (ReadOnlyMemory<byte>)result);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "__db_changes__ failed (requestId={RequestId})", context.RequestId);
                var error = MemoryPack.MemoryPackSerializer.Serialize(
                    DataContextResult.Failure("Remote DataContext mutation failed."));
                return (HttpStatusCode.InternalServerError, (ReadOnlyMemory<byte>)error);
            }
        });

        client.RegisterStreamHandler("__db_query_stream__", async stream =>
        {
            logger.LogWarning(
                "Rejected remote DataContext stream {StreamId}: Bolt stream callbacks do not expose authenticated sender provenance.",
                stream.StreamId);
            await stream.CloseAsync(HttpStatusCode.Forbidden);
        });

        logger.LogInformation("Registered DataContext Bolt handlers (__db_query__, __db_changes__, __db_query_stream__)");
    }

    private static async Task<TrustedServiceInvocationResult> AuthorizeAsync(
        IServiceProvider services,
        RequestMetadata? metadata,
        BoltInboundRequestContext context,
        IReadOnlyCollection<string> requiredScopes,
        CancellationToken ct)
    {
        var authorizer = services.GetRequiredService<IBoltServiceInvocationAuthorizer>();
        return await authorizer.AuthorizeAsync(metadata, context, requiredScopes, ct: ct);
    }

    private static ReadOnlyMemory<byte> SerializeFailure(string? message) =>
        MemoryPack.MemoryPackSerializer.Serialize(
            DataContextResult.Failure(message ?? "Remote DataContext authorization failed"));
}
