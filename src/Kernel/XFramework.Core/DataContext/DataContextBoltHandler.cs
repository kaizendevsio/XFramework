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
            var policyRegistry = scope.ServiceProvider.GetRequiredService<GeneratedEntityAuthorizationPolicyRegistry>();
            try
            {
                var envelope = DeserializeEnvelope(payload);
                var descriptor = MemoryPack.MemoryPackSerializer.Deserialize<QueryDescriptor>(envelope.Payload);
                if (descriptor is null)
                    return (HttpStatusCode.BadRequest, SerializeFailure("Invalid remote DataContext query."));

                if (!policyRegistry.TryGet(
                        descriptor.EntityTypeName,
                        GeneratedEntityOperation.Read,
                        out var entityPolicy) ||
                    !entityPolicy.AllowRemoteQuery)
                {
                    return (HttpStatusCode.Forbidden, SerializeFailure("Remote DataContext access is not authorized."));
                }

                descriptor.Metadata ??= new RequestMetadata();
                var authorization = await AuthorizeAsync(
                    scope.ServiceProvider,
                    envelope,
                    descriptor.Metadata,
                    context,
                    [entityPolicy],
                    descriptor.IgnoreQueryFilters
                        ? [XFrameworkServiceScopes.DataContextQuery, XFrameworkServiceScopes.DataContextQueryAllTenants]
                        : [XFrameworkServiceScopes.DataContextQuery],
                    descriptor.IgnoreQueryFilters
                        ? ["identity.tenants:manage"]
                        : [],
                    ct);
                if (!authorization.IsSuccess)
                    return ((HttpStatusCode)authorization.StatusCode, SerializeFailure(authorization.Error, authorization.StatusCode));

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
            var policyRegistry = scope.ServiceProvider.GetRequiredService<GeneratedEntityAuthorizationPolicyRegistry>();
            try
            {
                var envelope = DeserializeEnvelope(payload);
                var request = MemoryPack.MemoryPackSerializer.Deserialize<SaveChangesRequest>(envelope.Payload);
                if (request is null)
                    return (HttpStatusCode.BadRequest, SerializeFailure("Invalid remote DataContext mutation."));

                var entityPolicies = new List<GeneratedEntityAuthorizationPolicy>();
                foreach (var change in request.Changes ?? [])
                {
                    if (!policyRegistry.TryGet(
                            change.EntityTypeName,
                            ToGeneratedOperation(change.Operation),
                            out var entityPolicy) ||
                        !entityPolicy.AllowRemoteMutation)
                    {
                        return (HttpStatusCode.Forbidden, SerializeFailure("Remote DataContext access is not authorized."));
                    }

                    entityPolicies.Add(entityPolicy);
                }

                request.Metadata ??= new RequestMetadata();
                var authorization = await AuthorizeAsync(
                    scope.ServiceProvider,
                    envelope,
                    request.Metadata,
                    context,
                    entityPolicies,
                    [XFrameworkServiceScopes.DataContextMutate],
                    [],
                    ct);
                if (!authorization.IsSuccess)
                    return ((HttpStatusCode)authorization.StatusCode, SerializeFailure(authorization.Error, authorization.StatusCode));

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
        IReadOnlyCollection<GeneratedEntityAuthorizationPolicy> entityPolicies,
        IReadOnlyCollection<string> requiredScopes,
        IReadOnlyCollection<string> requiredActorCapabilities,
        CancellationToken ct)
    {
        var authorizer = services.GetRequiredService<IBoltServiceInvocationAuthorizer>();
        var hasActor = !string.IsNullOrWhiteSpace(envelope.ActorAccessToken);
        if (!hasActor && entityPolicies.Any(policy => !policy.AllowServiceOnly))
            return TrustedInvocationResult.Failure("Actor identity is required.");

        if (!hasActor && requiredActorCapabilities.Count > 0)
            return TrustedInvocationResult.Failure("Actor is not authorized for this operation.", 403);

        var hasTenantlessPolicy = entityPolicies.Any(static policy =>
            policy.TenantAccessMode == TenantAccessMode.Tenantless);
        if (hasTenantlessPolicy && entityPolicies.Any(static policy =>
                policy.TenantAccessMode != TenantAccessMode.Tenantless))
        {
            return TrustedInvocationResult.Failure(
                "A remote DataContext request cannot mix tenantless and tenant-scoped entities.",
                403);
        }

        var actorTenantMode = hasTenantlessPolicy
            ? TenantAccessMode.Tenantless
            : entityPolicies.Any(static policy => policy.TenantAccessMode == TenantAccessMode.DelegatedTenant)
                ? TenantAccessMode.DelegatedTenant
                : TenantAccessMode.ActorTenant;
        var crossTenantCapabilities = entityPolicies
            .Where(static policy => policy.TenantAccessMode == TenantAccessMode.DelegatedTenant)
            .SelectMany(static policy => policy.RequiredCrossTenantActorCapabilities)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var policy = hasActor
            ? new InvocationAuthorizationPolicy
            {
                ActorRequirement = ActorRequirement.Required,
                TenantAccessMode = actorTenantMode,
                RequiredServiceScopes = requiredScopes,
                AllowedServiceCallers = XFrameworkServiceNames.All,
                RequiredActorCapabilities = requiredActorCapabilities,
                RequiredCrossTenantActorCapabilities = crossTenantCapabilities
            }
            : new InvocationAuthorizationPolicy
            {
                ActorRequirement = ActorRequirement.None,
                TenantAccessMode = hasTenantlessPolicy
                    ? TenantAccessMode.Tenantless
                    : TenantAccessMode.ServiceTargetTenant,
                RequiredServiceScopes = requiredScopes
                    .Concat(hasTenantlessPolicy ? [] : [XFrameworkServiceScopes.TenantTarget])
                    .Concat(entityPolicies.SelectMany(static entityPolicy => entityPolicy.RequiredServiceScopes))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                AllowedServiceCallers = entityPolicies
                    .SelectMany(static entityPolicy => entityPolicy.AllowedServiceCallers)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray()
            };
        var authorization = await authorizer.AuthorizeAsync(
            new InvocationCredentials(envelope.ActorAccessToken, envelope.ServiceAccessToken),
            metadata,
            context,
            policy,
            ct);
        if (!authorization.IsSuccess)
            return authorization;

        foreach (var entityPolicy in entityPolicies)
        {
            var entityAuthorization = GeneratedEntityAuthorizationEvaluator.Evaluate(
                authorization.Context,
                entityPolicy);
            if (!entityAuthorization.IsSuccess)
            {
                return TrustedInvocationResult.Failure(
                    entityAuthorization.Error!,
                    entityAuthorization.StatusCode);
            }
        }

        return authorization;
    }

    private static GeneratedEntityOperation ToGeneratedOperation(ChangeOperation operation) =>
        operation switch
        {
            ChangeOperation.Add => GeneratedEntityOperation.Create,
            ChangeOperation.Update => GeneratedEntityOperation.Update,
            ChangeOperation.Remove => GeneratedEntityOperation.Delete,
            _ => throw new InvalidOperationException("Unsupported remote DataContext operation.")
        };

    private static BoltInvocationEnvelope DeserializeEnvelope(ReadOnlyMemory<byte> payload) =>
        MemoryPack.MemoryPackSerializer.Deserialize<BoltInvocationEnvelope>(payload.Span)
        ?? throw new InvalidOperationException("Bolt invocation envelope is required.");

    private static ReadOnlyMemory<byte> SerializeFailure(string? message, int statusCode = 400) =>
        MemoryPack.MemoryPackSerializer.Serialize(
            DataContextResult.Failure(
                message ?? "Remote DataContext authorization failed",
                statusCode));
}
