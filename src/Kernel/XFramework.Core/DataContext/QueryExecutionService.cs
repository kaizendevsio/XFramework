using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using IdentityServer.Domain.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using XFramework.Core.Services.FeatureGates;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Base;
using XFramework.Domain.Shared.DataContext;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Security;

namespace XFramework.Core.DataContext;

public sealed class QueryExecutionService(
    IServiceProvider serviceProvider,
    ILogger<QueryExecutionService> logger,
    ITrustedInvocationContextAccessor invocationContextAccessor,
    GeneratedEntityAuthorizationPolicyRegistry? authorizationPolicyRegistry = null)
    : IQueryExecutionService
{
    public const int MaximumMutationBatchSize = 100;
    public const int MaximumSerializedEntitySizeBytes = 1024 * 1024;
    public const int MaximumSaveChangesRequestSizeBytes = 8 * 1024 * 1024;

    private static readonly HashSet<string> ProtectedRemoteUpdateProperties =
    [
        nameof(IHasId.Id),
        nameof(IHasTenantId.TenantId),
        nameof(IHasConcurrencyStamp.ConcurrencyStamp),
        "CreatedAt",
        "ModifiedAt",
        "IsDeleted",
        "DeletedAt",
        "IsEnabled"
    ];

    private readonly ConcurrentDictionary<string, Type> _entityTypes = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, bool> _mutableEntityTypes = new(StringComparer.OrdinalIgnoreCase);
    private readonly GeneratedEntityAuthorizationPolicyRegistry _authorizationPolicyRegistry =
        authorizationPolicyRegistry ?? new GeneratedEntityAuthorizationPolicyRegistry();

    public void RegisterEntity<T>(string name) where T : class
        => RegisterEntity(typeof(T), name);

    public void RegisterEntity(Type entityType, string name)
        => RegisterEntity(entityType, name, allowRemoteMutation: false);

    public void RegisterEntity(Type entityType, string name, bool allowRemoteMutation)
    {
        _entityTypes[name] = entityType;
        _mutableEntityTypes[name] = allowRemoteMutation;
        logger.LogDebug("Registered entity type '{Name}' -> {Type}", name, entityType.FullName);
    }

    public async Task<byte[]> ExecuteAsync(byte[] queryDescriptorBytes, CancellationToken ct = default)
    {
        if (queryDescriptorBytes.Length > QueryDescriptorExecutor.MaximumQueryDescriptorBytes)
            return SerializeError($"Remote query descriptor must not exceed {QueryDescriptorExecutor.MaximumQueryDescriptorBytes} bytes.");

        QueryDescriptor? descriptor;
        try
        {
            descriptor = MemoryPack.MemoryPackSerializer.Deserialize<QueryDescriptor>((ReadOnlySpan<byte>)queryDescriptorBytes);
        }
        catch (Exception)
        {
            return SerializeError("Failed to deserialize QueryDescriptor.");
        }

        if (descriptor is null)
            return SerializeError("Failed to deserialize QueryDescriptor.");

        if (!_entityTypes.TryGetValue(descriptor.EntityTypeName, out var entityType))
            return SerializeError($"Entity type '{descriptor.EntityTypeName}' is not registered. Query rejected.");

        var authorization = ValidateGeneratedAuthorization(
            descriptor.EntityTypeName,
            GeneratedEntityOperation.Read,
            requireRemoteQuery: true);
        if (!authorization.IsSuccess)
            return SerializeError(authorization.Error!, authorization.StatusCode);

        var descriptorError = QueryDescriptorExecutor.ValidateDescriptor(descriptor);
        if (descriptorError is not null)
            return SerializeError(descriptorError);

        var trust = ValidateTrustedDataContext(
            RequiredScopes(
                descriptor.IgnoreQueryFilters
                    ? [XFrameworkServiceScopes.DataContextQuery, XFrameworkServiceScopes.DataContextQueryAllTenants]
                    : [XFrameworkServiceScopes.DataContextQuery]),
            requireTenant: RequiresTenantMetadata(entityType));
        if (!trust.IsSuccess)
            return SerializeError(trust.Error!, trust.StatusCode);

        if (!HasRequiredQueryTenant(entityType))
            return SerializeError($"Entity type '{descriptor.EntityTypeName}' requires a trusted tenant for remote DataContext query.");

        var featureError = await ValidateTargetFeatureAsync(
            serviceProvider,
            descriptor.EntityTypeName,
            GeneratedEntityOperation.Read,
            ct);
        if (featureError is not null)
            return SerializeError(featureError, 403);

        try
        {
            var dbContext = serviceProvider.GetRequiredService<DbContext>();

            if (descriptor.IgnoreQueryFilters)
            {
                if (invocationContextAccessor.Current?.Actor is not { } actor ||
                    !actor.Capabilities.Contains("identity.tenants:manage"))
                    return SerializeError("Query-filter bypass requires manage capability.", 403);
            }

            var result = await QueryDescriptorExecutor.ExecuteAsync(
                dbContext,
                entityType,
                descriptor,
                invocationContextAccessor.Current?.EffectiveTenantId,
                ct,
                allowAllTenants: descriptor.IgnoreQueryFilters);

            // MemoryPackSerializer.Serialize(object?) uses the object formatter which fails
            // for runtime-typed results. Use the actual result type for correct serialization.
            var resultType = GetResultType(descriptor.Mode, entityType);
            return MemoryPack.MemoryPackSerializer.Serialize(resultType, result);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to execute query for entity '{EntityType}'", descriptor.EntityTypeName);
            return SerializeError("The requested query could not be completed.");
        }
    }

    private static Type GetResultType(QueryExecutionMode mode, Type entityType) => mode switch
    {
        QueryExecutionMode.ToList => typeof(List<>).MakeGenericType(entityType),
        QueryExecutionMode.FirstOrDefault or QueryExecutionMode.SingleOrDefault
            or QueryExecutionMode.MinBy or QueryExecutionMode.MaxBy => entityType,
        QueryExecutionMode.Count => typeof(int),
        QueryExecutionMode.Any or QueryExecutionMode.AnyWithPredicate or QueryExecutionMode.All => typeof(bool),
        QueryExecutionMode.Sum => typeof(decimal),
        QueryExecutionMode.Average => typeof(double),
        _ => typeof(object)
    };

    public async IAsyncEnumerable<byte[]> ExecuteStreamAsync(
        byte[] queryDescriptorBytes,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (queryDescriptorBytes.Length > QueryDescriptorExecutor.MaximumQueryDescriptorBytes)
            yield break;

        QueryDescriptor? descriptor;
        try
        {
            descriptor = MemoryPack.MemoryPackSerializer.Deserialize<QueryDescriptor>((ReadOnlySpan<byte>)queryDescriptorBytes);
        }
        catch (Exception)
        {
            yield break;
        }

        if (descriptor is null)
            yield break;

        if (!_entityTypes.TryGetValue(descriptor.EntityTypeName, out var entityType))
            yield break;

        if (!ValidateGeneratedAuthorization(
                descriptor.EntityTypeName,
                GeneratedEntityOperation.Read,
                requireRemoteQuery: true).IsSuccess)
            yield break;

        if (QueryDescriptorExecutor.ValidateDescriptor(descriptor) is not null)
            yield break;

        var trust = ValidateTrustedDataContext(
            RequiredScopes(
                descriptor.IgnoreQueryFilters
                    ? [XFrameworkServiceScopes.DataContextQuery, XFrameworkServiceScopes.DataContextQueryAllTenants]
                    : [XFrameworkServiceScopes.DataContextQuery]),
            requireTenant: RequiresTenantMetadata(entityType));
        if (!trust.IsSuccess)
            yield break;

        if (!HasRequiredQueryTenant(entityType))
            yield break;

        if (await ValidateTargetFeatureAsync(
                serviceProvider,
                descriptor.EntityTypeName,
                GeneratedEntityOperation.Read,
                ct) is not null)
            yield break;

        var dbContext = serviceProvider.GetRequiredService<DbContext>();

        if (descriptor.IgnoreQueryFilters)
        {
            if (invocationContextAccessor.Current?.Actor is not { } actor ||
                !actor.Capabilities.Contains("identity.tenants:manage"))
                yield break;
        }

        await foreach (var item in QueryDescriptorExecutor.ExecuteStreamAsync(
                           dbContext,
                           entityType,
                           descriptor,
                           invocationContextAccessor.Current?.EffectiveTenantId,
                           ct,
                           allowAllTenants: descriptor.IgnoreQueryFilters))
        {
            yield return MemoryPack.MemoryPackSerializer.Serialize(item);
        }
    }

    public async Task<byte[]> ExecuteChangesAsync(byte[] saveChangesRequestBytes, CancellationToken ct = default)
    {
        if (saveChangesRequestBytes.Length > MaximumSaveChangesRequestSizeBytes)
            return SerializeError($"Remote mutation request must not exceed {MaximumSaveChangesRequestSizeBytes} bytes.");

        SaveChangesRequest? request;
        try
        {
            request = MemoryPack.MemoryPackSerializer.Deserialize<SaveChangesRequest>((ReadOnlySpan<byte>)saveChangesRequestBytes);
        }
        catch (Exception)
        {
            return SerializeError("Failed to deserialize SaveChangesRequest.");
        }

        if (request is null)
            return SerializeError("Failed to deserialize SaveChangesRequest.");

        if (request.Changes is null)
            return SerializeError("Remote mutation changes are required.");

        if (request.Changes.Count > MaximumMutationBatchSize)
            return SerializeError($"Remote mutation supports at most {MaximumMutationBatchSize} changes per request.");

        if (request.Changes.Any(change =>
                change.SerializedEntity is null ||
                change.SerializedEntity.Length > MaximumSerializedEntitySizeBytes))
        {
            return SerializeError($"Remote mutation entities must not exceed {MaximumSerializedEntitySizeBytes} bytes.");
        }

        var preflight = new List<(ChangeEntry Change, Type EntityType, GeneratedEntityOperation Operation)>();
        foreach (var change in request.Changes)
        {
            if (!_entityTypes.TryGetValue(change.EntityTypeName, out var entityType))
                return SerializeError($"Entity type '{change.EntityTypeName}' is not registered.", 403);

            if (!_mutableEntityTypes.TryGetValue(change.EntityTypeName, out var canMutate) || !canMutate)
                return SerializeError($"Entity type '{change.EntityTypeName}' is not registered for remote mutation.", 403);

            var operation = ResolveGeneratedOperation(change.Operation);
            var authorization = ValidateGeneratedAuthorization(
                change.EntityTypeName,
                operation,
                requireRemoteMutation: true);
            if (!authorization.IsSuccess)
                return SerializeError(authorization.Error!, authorization.StatusCode);

            var trust = ValidateTrustedDataContext(
                RequiredScopes([XFrameworkServiceScopes.DataContextMutate]),
                requireTenant: RequiresTenantMetadata(entityType));
            if (!trust.IsSuccess)
                return SerializeError(trust.Error!, trust.StatusCode);

            preflight.Add((change, entityType, operation));
        }

        foreach (var featureCheck in preflight
                     .Select(static item => (item.Change.EntityTypeName, item.Operation))
                     .Distinct())
        {
            var featureError = await ValidateTargetFeatureAsync(
                serviceProvider,
                featureCheck.EntityTypeName,
                featureCheck.Operation,
                ct);
            if (featureError is not null)
                return SerializeError(featureError, 403);
        }

        try
        {
            var dbContext = serviceProvider.GetRequiredService<DbContext>();
            var changedTenantModuleFeatures = new List<(Guid TenantId, string ModuleKey, string SubFeatureKey)>();
            var persistedEntities = new List<(string EntityTypeName, object Entity)>();

            foreach (var (change, entityType, _) in preflight)
            {
                // For Update operations, try FieldPatch first before deserializing as entity
                if (change.Operation == ChangeOperation.Update)
                {
                    FieldPatch? patch = null;
                    try
                    {
                        patch = MemoryPack.MemoryPackSerializer.Deserialize<FieldPatch>(
                            (ReadOnlySpan<byte>)change.SerializedEntity);
                    }
                    catch { }

                    if (patch is { EntityId.Length: > 0, Changes.Count: > 0 })
                    {
                        var pkValue = MemoryPack.MemoryPackSerializer.Deserialize<Guid>(
                            (ReadOnlySpan<byte>)patch.EntityId);
                        var existing = await dbContext.FindAsync(entityType, [pkValue], ct);
                        if (existing is null)
                            return SerializeError($"Entity '{change.EntityTypeName}' with PK '{pkValue}' not found.");

                        var existingEntry = dbContext.Entry(existing);
                        if (existingEntry.State == EntityState.Detached)
                        {
                            dbContext.Attach(existing);
                            existingEntry = dbContext.Entry(existing);
                        }

                        if (!ValidateTenantContext(existing, entityType, out var patchMetadataError))
                            return SerializeError(patchMetadataError);

                        if (existing is IHasConcurrencyStamp patchStamped)
                        {
                            if (patch.ExpectedConcurrencyStamp is not { } expectedStamp || expectedStamp == Guid.Empty)
                                return SerializeError($"Entity '{change.EntityTypeName}' requires a concurrency stamp for remote update.");

                            var concurrencyProperty = existingEntry
                                .Property(nameof(IHasConcurrencyStamp.ConcurrencyStamp));
                            concurrencyProperty.OriginalValue = expectedStamp;
                            patchStamped.ConcurrencyStamp = Guid.NewGuid();
                            concurrencyProperty.IsModified = true;
                        }

                        foreach (var (propertyName, valueBytes) in patch.Changes)
                        {
                            var prop = entityType.GetProperty(propertyName);
                            if (prop is null) continue;

                            if (ProtectedRemoteUpdateProperties.Contains(prop.Name))
                                return SerializeError(
                                    $"Entity '{change.EntityTypeName}' property '{prop.Name}' cannot be changed through remote DataContext.");

                            var value = MemoryPack.MemoryPackSerializer.Deserialize(
                                prop.PropertyType, (ReadOnlySpan<byte>)valueBytes);
                            prop.SetValue(existing, value);
                            existingEntry.Property(prop.Name).IsModified = true;
                        }

                        var patchValidationError = await ValidateRemoteMutationAsync(
                            serviceProvider,
                            entityType,
                            existing,
                            ct);
                        if (patchValidationError is not null)
                            return SerializeError(patchValidationError);

                        if (existing is TenantModuleFeature patchedFeature)
                        {
                            changedTenantModuleFeatures.Add((
                                patchedFeature.TenantId,
                                patchedFeature.ModuleKey,
                                patchedFeature.SubFeatureKey));
                        }

                        persistedEntities.Add((change.EntityTypeName, existing));

                        continue;
                    }

                    return SerializeError(
                        $"Entity '{change.EntityTypeName}' remote updates require a field patch.");
                }

                // Deserialize as full entity for Add, Remove, or Update fallback
                var entity = MemoryPack.MemoryPackSerializer.Deserialize(entityType, (ReadOnlySpan<byte>)change.SerializedEntity);
                if (entity is null)
                    return SerializeError($"Failed to deserialize entity of type '{change.EntityTypeName}'.");

                if (change.Operation == ChangeOperation.Remove)
                {
                    if (entity is not IHasId requestedEntity || requestedEntity.Id == Guid.Empty)
                        return SerializeError($"Entity '{change.EntityTypeName}' requires a valid ID for remote deletion.");

                    var persisted = await dbContext.FindAsync(entityType, [requestedEntity.Id], ct);
                    if (persisted is null)
                    {
                        return SerializeError(
                            $"Entity '{change.EntityTypeName}' with PK '{requestedEntity.Id}' not found.");
                    }

                    if (!ValidateTenantContext(persisted, entityType, out var persistedTenantError))
                        return SerializeError(persistedTenantError);

                    var removeValidationError = await ValidateRemoteMutationAsync(
                        serviceProvider,
                        entityType,
                        persisted,
                        ct);
                    if (removeValidationError is not null)
                        return SerializeError(removeValidationError);

                    if (persisted is IHasConcurrencyStamp persistedStamp)
                    {
                        if (entity is not IHasConcurrencyStamp requestedStamp ||
                            requestedStamp.ConcurrencyStamp == Guid.Empty)
                        {
                            return SerializeError(
                                $"Entity '{change.EntityTypeName}' requires a concurrency stamp for remote mutation.");
                        }

                        dbContext.Entry(persisted)
                            .Property(nameof(IHasConcurrencyStamp.ConcurrencyStamp))
                            .OriginalValue = requestedStamp.ConcurrencyStamp;
                    }

                    dbContext.Remove(persisted);
                    persistedEntities.Add((change.EntityTypeName, persisted));

                    if (persisted is TenantModuleFeature removedFeature)
                    {
                        changedTenantModuleFeatures.Add((
                            removedFeature.TenantId,
                            removedFeature.ModuleKey,
                            removedFeature.SubFeatureKey));
                    }

                    continue;
                }

                if (!ValidateTenantContext(entity, entityType, out var metadataError))
                    return SerializeError(metadataError);

                var validationError = await ValidateRemoteMutationAsync(
                    serviceProvider,
                    entityType,
                    entity,
                    ct);
                if (validationError is not null)
                    return SerializeError(validationError);

                if (entity is IHasConcurrencyStamp stampedEntity)
                {
                    if (change.Operation is ChangeOperation.Update or ChangeOperation.Remove)
                    {
                        if (stampedEntity.ConcurrencyStamp == Guid.Empty)
                            return SerializeError($"Entity '{change.EntityTypeName}' requires a concurrency stamp for remote mutation.");

                        dbContext.Entry(entity)
                            .Property(nameof(IHasConcurrencyStamp.ConcurrencyStamp))
                            .OriginalValue = stampedEntity.ConcurrencyStamp;
                    }

                    if (change.Operation is ChangeOperation.Add or ChangeOperation.Update)
                        stampedEntity.ConcurrencyStamp = Guid.NewGuid();
                }

                switch (change.Operation)
                {
                    case ChangeOperation.Add:
                        dbContext.Add(entity);
                        break;
                    case ChangeOperation.Update:
                        dbContext.Update(entity);
                        break;
                    case ChangeOperation.Remove:
                        dbContext.Remove(entity);
                        break;
                }

                persistedEntities.Add((change.EntityTypeName, entity));

                if (entity is TenantModuleFeature feature)
                {
                    changedTenantModuleFeatures.Add((
                        feature.TenantId,
                        feature.ModuleKey,
                        feature.SubFeatureKey));
                }
            }

            await dbContext.SaveChangesAsync(ct);
            InvalidateTenantModuleFeatureCache(serviceProvider, changedTenantModuleFeatures);

            var persistedStates = persistedEntities.Select(static persisted =>
            {
                var identified = (IHasId)persisted.Entity;
                return new PersistedEntityState
                {
                    EntityTypeName = persisted.EntityTypeName,
                    EntityId = identified.Id,
                    ConcurrencyStamp = (persisted.Entity as IHasConcurrencyStamp)?.ConcurrencyStamp
                };
            }).ToList();
            var result = DataContextResult.Success(persistedEntities: persistedStates);
            return MemoryPack.MemoryPackSerializer.Serialize(result);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            logger.LogWarning(
                ex,
                "Remote DataContext concurrency conflict for tenant {TenantId}; change count {ChangeCount}; entity types {EntityTypes}",
                invocationContextAccessor.Current?.EffectiveTenantId,
                request.Changes.Count,
                string.Join(", ", request.Changes.Select(static change => change.EntityTypeName).Distinct()));
            var result = DataContextResult.Failure(
                "The requested data changed after it was loaded. Reload and try again.",
                409);
            return MemoryPack.MemoryPackSerializer.Serialize(result);
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(
                ex,
                "Remote DataContext mutation failed for tenant {TenantId}; change count {ChangeCount}; entity types {EntityTypes}",
                invocationContextAccessor.Current?.EffectiveTenantId,
                request.Changes.Count,
                string.Join(", ", request.Changes.Select(static change => change.EntityTypeName).Distinct()));
            var result = DataContextResult.Failure(
                "The requested data change conflicts with existing data or constraints.");
            return MemoryPack.MemoryPackSerializer.Serialize(result);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Remote DataContext mutation failed for tenant {TenantId}; change count {ChangeCount}; entity types {EntityTypes}",
                invocationContextAccessor.Current?.EffectiveTenantId,
                request.Changes.Count,
                string.Join(", ", request.Changes.Select(static change => change.EntityTypeName).Distinct()));
            return MemoryPack.MemoryPackSerializer.Serialize(
                DataContextResult.Failure("The requested data change could not be completed.", 500));
        }
    }

    private bool ValidateTenantContext(
        object entity,
        Type entityType,
        out string error)
    {
        error = string.Empty;

        if (entity is not IHasTenantId tenantEntity)
            return true;

        if (invocationContextAccessor.Current?.EffectiveTenantId is not { } effectiveTenantId ||
            effectiveTenantId == Guid.Empty)
        {
            error = $"Entity '{entityType.Name}' requires a trusted tenant for remote DataContext mutation.";
            return false;
        }

        if (entity is Tenant tenant)
        {
            if (tenant.Id == effectiveTenantId)
                return true;

            error = $"Entity '{entityType.Name}' Id does not match the trusted invocation tenant.";
            return false;
        }

        if (tenantEntity.TenantId == Guid.Empty)
        {
            tenantEntity.TenantId = effectiveTenantId;
            return true;
        }

        if (tenantEntity.TenantId == effectiveTenantId)
            return true;

        error = $"Entity '{entityType.Name}' TenantId does not match the trusted invocation tenant.";
        return false;
    }

    private bool HasRequiredQueryTenant(Type entityType) =>
        !RequiresTenantMetadata(entityType) ||
        invocationContextAccessor.Current?.EffectiveTenantId is { } tenantId && tenantId != Guid.Empty;

    private static bool RequiresTenantMetadata(Type entityType) =>
        typeof(IHasTenantId).IsAssignableFrom(entityType);

    private IReadOnlyCollection<string> RequiredScopes(IReadOnlyCollection<string> operationScopes)
    {
        if (invocationContextAccessor.Current?.Actor is not null)
            return operationScopes;

        return operationScopes
            .Append(XFrameworkServiceScopes.TenantTarget)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private InvocationPolicyCheckResult ValidateTrustedDataContext(
        IReadOnlyCollection<string> scopes,
        bool requireTenant)
    {
        var context = invocationContextAccessor.Current;
        if (context?.Service is null)
            return InvocationPolicyCheckResult.Failure(
                "A trusted service identity is required for remote DataContext access.",
                401);
        if (!XFrameworkServiceNames.All.Contains(context.Service.ClientId, StringComparer.OrdinalIgnoreCase))
            return InvocationPolicyCheckResult.Failure(
                "The trusted service identity is not an allowed remote DataContext caller.",
                403);
        if (requireTenant &&
            (context.EffectiveTenantId is not { } tenantId || tenantId == Guid.Empty))
            return InvocationPolicyCheckResult.Failure(
                "A trusted tenant is required for remote DataContext access.",
                403);

        if (scopes.Contains(XFrameworkServiceScopes.DataContextQueryAllTenants, StringComparer.OrdinalIgnoreCase) &&
            context.Actor is null)
        {
            return InvocationPolicyCheckResult.Failure(
                "Query-filter bypass requires an authorized actor.",
                401);
        }

        if (scopes.Contains(XFrameworkServiceScopes.DataContextQueryAllTenants, StringComparer.OrdinalIgnoreCase) &&
            !context.Actor!.Capabilities.Contains("identity.tenants:manage"))
        {
            return InvocationPolicyCheckResult.Failure(
                "Query-filter bypass requires an authorized actor.",
                403);
        }

        var missingScopes = scopes.Where(scope => !context.Service.Scopes.Contains(scope)).ToArray();
        return missingScopes.Length == 0
            ? InvocationPolicyCheckResult.Success()
            : InvocationPolicyCheckResult.Failure(
                $"Trusted service identity is missing required scope(s): {string.Join(", ", missingScopes)}.",
                403);
    }

    private async Task<string?> ValidateTargetFeatureAsync(
        IServiceProvider scopedServices,
        string entityTypeName,
        GeneratedEntityOperation operation,
        CancellationToken ct)
    {
        if (!_authorizationPolicyRegistry.TryGet(
                entityTypeName,
                operation,
                out var policy) ||
            string.IsNullOrWhiteSpace(policy.AuthorizationFeature) ||
            invocationContextAccessor.Current?.EffectiveTenantId is not { } targetTenantId ||
            targetTenantId == Guid.Empty)
            return null;

        var feature = TenantModuleFeatureKeys.Normalize(policy.AuthorizationFeature);

        var featureService = scopedServices.GetService<ITenantModuleFeatureService>();
        if (featureService is null)
            return "Tenant feature validation is unavailable.";

        var result = await featureService.EnsureEnabledAsync(
            targetTenantId,
            feature.ModuleKey,
            feature.SubFeatureKey,
            ct);
        return result.IsSuccess ? null : "The requested tenant feature is not enabled.";
    }

    private static async Task<string?> ValidateRemoteMutationAsync(
        IServiceProvider scopedServices,
        Type entityType,
        object entity,
        CancellationToken ct)
    {
        var validator = scopedServices
            .GetServices<IRemoteDataContextEntityValidator>()
            .SingleOrDefault(candidate => candidate.EntityType == entityType);
        if (validator is null)
            return null;

        var errors = await validator.ValidateAsync(entity, ct);
        return errors.Count == 0
            ? null
            : $"Entity validation failed: {string.Join("; ", errors)}";
    }

    private static GeneratedEntityOperation ResolveGeneratedOperation(ChangeOperation operation) => operation switch
    {
        ChangeOperation.Add => GeneratedEntityOperation.Create,
        ChangeOperation.Update => GeneratedEntityOperation.Update,
        ChangeOperation.Remove => GeneratedEntityOperation.Delete,
        _ => throw new InvalidOperationException("Unsupported remote DataContext operation.")
    };

    private InvocationPolicyCheckResult ValidateGeneratedAuthorization(
        string entityTypeName,
        GeneratedEntityOperation operation,
        bool requireRemoteQuery = false,
        bool requireRemoteMutation = false)
    {
        if (!_authorizationPolicyRegistry.TryGet(entityTypeName, operation, out var policy))
            return InvocationPolicyCheckResult.Failure("Remote DataContext access is not authorized.", 403);

        if ((requireRemoteQuery && !policy.AllowRemoteQuery) ||
            (requireRemoteMutation && !policy.AllowRemoteMutation))
        {
            return InvocationPolicyCheckResult.Failure("Remote DataContext access is not authorized.", 403);
        }

        return GeneratedEntityAuthorizationEvaluator.Evaluate(
            invocationContextAccessor.Current,
            policy);
    }

    private static void InvalidateTenantModuleFeatureCache(
        IServiceProvider scopedServices,
        IEnumerable<(Guid TenantId, string ModuleKey, string SubFeatureKey)> changedFeatures)
    {
        var featureService = scopedServices.GetService<ITenantModuleFeatureService>();
        if (featureService is null)
            return;

        foreach (var feature in changedFeatures.Distinct())
        {
            featureService.Invalidate(feature.TenantId, feature.ModuleKey, feature.SubFeatureKey);
        }
    }

    private static byte[] SerializeError(string message, int statusCode = 400)
    {
        var result = DataContextResult.Failure(message, statusCode);
        return MemoryPack.MemoryPackSerializer.Serialize(result);
    }
}
