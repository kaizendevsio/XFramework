using System.Net;
using Bolt.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using XFramework.Domain.Shared.DataContext;
using XFramework.Integration.Abstractions;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Security;
using XFramework.Domain.Shared.BusinessObjects;

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
                var envelope = DeserializeEnvelope(payload);
                var descriptor = MemoryPack.MemoryPackSerializer.Deserialize<QueryDescriptor>(envelope.Payload);
                if (descriptor is null)
                    return (HttpStatusCode.BadRequest, SerializeFailure("Invalid remote DataContext query."));

                descriptor.Metadata ??= new RequestMetadata();
                var authorization = await AuthorizeAsync(
                    scope.ServiceProvider,
                    envelope,
                    descriptor.Metadata,
                    context,
                    descriptor.IgnoreQueryFilters
                        ? [XFrameworkServiceScopes.DataContextQuery, XFrameworkServiceScopes.DataContextQueryAllTenants]
                        : [XFrameworkServiceScopes.DataContextQuery],
                    descriptor.IgnoreQueryFilters
                        ? ["identity.tenants:manage"]
                        : [],
                    ct);
                if (!authorization.IsSuccess)
                    return ((HttpStatusCode)authorization.StatusCode, SerializeFailure(authorization.Error));

                var result = await queryService.ExecuteAsync(envelope.Payload, ct);
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
                var envelope = DeserializeEnvelope(payload);
                var request = MemoryPack.MemoryPackSerializer.Deserialize<SaveChangesRequest>(envelope.Payload);
                if (request is null)
                    return (HttpStatusCode.BadRequest, SerializeFailure("Invalid remote DataContext mutation."));

                request.Metadata ??= new RequestMetadata();
                var authorization = await AuthorizeAsync(
                    scope.ServiceProvider,
                    envelope,
                    request.Metadata,
                    context,
                    [XFrameworkServiceScopes.DataContextMutate],
                    [],
                    ct);
                if (!authorization.IsSuccess)
                    return ((HttpStatusCode)authorization.StatusCode, SerializeFailure(authorization.Error));

                var result = await queryService.ExecuteChangesAsync(envelope.Payload, ct);
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

        // Bolt stream callbacks do not expose authenticated sender provenance. Leaving this command
        // unregistered makes Bolt reject it consistently with 501 Not Implemented.
        logger.LogInformation("Registered DataContext Bolt handlers (__db_query__, __db_changes__); streaming is unsupported");
    }

    private static async Task<TrustedInvocationResult> AuthorizeAsync(
        IServiceProvider services,
        BoltInvocationEnvelope envelope,
        RequestMetadata metadata,
        BoltInboundRequestContext context,
        IReadOnlyCollection<string> requiredScopes,
        IReadOnlyCollection<string> requiredActorCapabilities,
        CancellationToken ct)
    {
        var authorizer = services.GetRequiredService<IBoltServiceInvocationAuthorizer>();
        var hasActor = !string.IsNullOrWhiteSpace(envelope.ActorAccessToken);
        var policy = hasActor
            ? new InvocationAuthorizationPolicy
            {
                ActorRequirement = ActorRequirement.Required,
                TenantAccessMode = TenantAccessMode.DelegatedTenant,
                RequiredServiceScopes = requiredScopes,
                RequiredActorCapabilities = requiredActorCapabilities,
                RequiredCrossTenantActorCapabilities = ["identity.tenants:manage"]
            }
            : new InvocationAuthorizationPolicy
            {
                ActorRequirement = ActorRequirement.None,
                TenantAccessMode = TenantAccessMode.ServiceTargetTenant,
                RequiredServiceScopes = requiredScopes
                    .Append(XFrameworkServiceScopes.TenantTarget)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                AllowedServiceCallers = XFrameworkServiceNames.All
            };
        return await authorizer.AuthorizeAsync(
            new InvocationCredentials(envelope.ActorAccessToken, envelope.ServiceAccessToken),
            metadata,
            context,
            policy,
            ct);
    }

    private static BoltInvocationEnvelope DeserializeEnvelope(ReadOnlyMemory<byte> payload) =>
        MemoryPack.MemoryPackSerializer.Deserialize<BoltInvocationEnvelope>(payload.Span)
        ?? throw new InvalidOperationException("Bolt invocation envelope is required.");

    private static ReadOnlyMemory<byte> SerializeFailure(string? message) =>
        MemoryPack.MemoryPackSerializer.Serialize(
            DataContextResult.Failure(message ?? "Remote DataContext authorization failed"));
}
