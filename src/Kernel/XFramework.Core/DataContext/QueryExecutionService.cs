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
    IConfiguration? configuration = null,
    ITrustedServiceInvocationResolver? serviceInvocationResolver = null)
    : IQueryExecutionService
{
    public const int MaximumMutationBatchSize = 100;
    public const int MaximumSerializedEntitySizeBytes = 1024 * 1024;
    public const int MaximumSaveChangesRequestSizeBytes = 8 * 1024 * 1024;

    private static readonly HashSet<string> ProtectedRemoteUpdateProperties =
    [
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

    public void RegisterEntity<T>(string name) where T : class
        => RegisterEntity(typeof(T), name);

    public void RegisterEntity(Type entityType, string name)
        => RegisterEntity(entityType, name, allowRemoteMutation: false);

    public void RegisterEntity(Type entityType, string name, bool allowRemoteMutation)
    {
        _entityTypes[name] = entityType;
        _mutableEntityTypes[name] = allowRemoteMutation;
        logger.LogDebug("Registered entity type '{Name}' → {Type}", name, entityType.FullName);
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

        var descriptorError = QueryDescriptorExecutor.ValidateDescriptor(descriptor);
        if (descriptorError is not null)
            return SerializeError(descriptorError);

        var trustError = await ValidateTrustedDataContextAsync(
            descriptor.Metadata,
            descriptor.IgnoreQueryFilters
                ? [XFrameworkServiceScopes.DataContextQuery, XFrameworkServiceScopes.DataContextQueryAllTenants]
                : [XFrameworkServiceScopes.DataContextQuery],
            requireTenant: RequiresTenantMetadata(entityType),
            ct);
        if (trustError is not null)
            return SerializeError(trustError);

        if (!HasRequiredQueryTenantMetadata(entityType, descriptor.Metadata))
            return SerializeError($"Entity type '{descriptor.EntityTypeName}' requires tenant metadata for remote DataContext query.");

        try
        {
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<DbContext>();

            var capabilityError = await ValidateCredentialCapabilityAsync(
                scope.ServiceProvider,
                entityType,
                descriptor.Metadata,
                IdentityAuthorizationConstants.View,
                ct);
            if (capabilityError is not null)
                return SerializeError(capabilityError);

            if (descriptor.IgnoreQueryFilters)
            {
                var bypassCapabilityError = await ValidateCredentialCapabilityAsync(
                    scope.ServiceProvider,
                    entityType,
                    descriptor.Metadata,
                    IdentityAuthorizationConstants.Manage,
                    ct);
                if (bypassCapabilityError is not null)
                    return SerializeError("Query-filter bypass requires manage capability.");
            }

            var featureError = await ValidateTargetFeatureAsync(
                scope.ServiceProvider,
                entityType,
                descriptor.Metadata,
                ct);
            if (featureError is not null)
                return SerializeError(featureError);

            var result = await QueryDescriptorExecutor.ExecuteAsync(dbContext, entityType, descriptor, ct);

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

        if (QueryDescriptorExecutor.ValidateDescriptor(descriptor) is not null)
            yield break;

        var trustError = await ValidateTrustedDataContextAsync(
            descriptor.Metadata,
            descriptor.IgnoreQueryFilters
                ? [XFrameworkServiceScopes.DataContextQuery, XFrameworkServiceScopes.DataContextQueryAllTenants]
                : [XFrameworkServiceScopes.DataContextQuery],
            requireTenant: RequiresTenantMetadata(entityType),
            ct);
        if (trustError is not null)
            yield break;

        if (!HasRequiredQueryTenantMetadata(entityType, descriptor.Metadata))
            yield break;

        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DbContext>();

        var capabilityError = await ValidateCredentialCapabilityAsync(
            scope.ServiceProvider,
            entityType,
            descriptor.Metadata,
            IdentityAuthorizationConstants.View,
            ct);
        if (capabilityError is not null)
            yield break;

        if (descriptor.IgnoreQueryFilters)
        {
            var bypassCapabilityError = await ValidateCredentialCapabilityAsync(
                scope.ServiceProvider,
                entityType,
                descriptor.Metadata,
                IdentityAuthorizationConstants.Manage,
                ct);
            if (bypassCapabilityError is not null)
                yield break;
        }

        var featureError = await ValidateTargetFeatureAsync(
            scope.ServiceProvider,
            entityType,
            descriptor.Metadata,
            ct);
        if (featureError is not null)
            yield break;

        await foreach (var item in QueryDescriptorExecutor.ExecuteStreamAsync(dbContext, entityType, descriptor, ct))
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

        try
        {
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<DbContext>();
            var changedTenantModuleFeatures = new List<(Guid TenantId, string ModuleKey, string SubFeatureKey)>();
            var persistedEntities = new List<(string EntityTypeName, object Entity)>();

            foreach (var change in request.Changes)
            {
                if (!_entityTypes.TryGetValue(change.EntityTypeName, out var entityType))
                    return SerializeError($"Entity type '{change.EntityTypeName}' is not registered.");

                if (!_mutableEntityTypes.TryGetValue(change.EntityTypeName, out var canMutate) || !canMutate)
                    return SerializeError($"Entity type '{change.EntityTypeName}' is not registered for remote mutation.");

                var trustError = await ValidateTrustedDataContextAsync(
                    request.Metadata,
                    [XFrameworkServiceScopes.DataContextMutate],
                    requireTenant: RequiresTenantMetadata(entityType),
                    ct);
                if (trustError is not null)
                    return SerializeError(trustError);

                var capabilityError = await ValidateCredentialCapabilityAsync(
                    scope.ServiceProvider,
                    entityType,
                    request.Metadata,
                    ResolveMutationCapability(change.Operation),
                    ct);
                if (capabilityError is not null)
                    return SerializeError(capabilityError);

                var featureError = await ValidateTargetFeatureAsync(
                    scope.ServiceProvider,
                    entityType,
                    request.Metadata,
                    ct);
                if (featureError is not null)
                    return SerializeError(featureError);

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

                        if (!ValidateTenantMetadata(existing, request.Metadata, entityType, out var patchMetadataError))
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
                            scope.ServiceProvider,
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

                if (!ValidateTenantMetadata(entity, request.Metadata, entityType, out var metadataError))
                    return SerializeError(metadataError);

                var validationError = await ValidateRemoteMutationAsync(
                    scope.ServiceProvider,
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
            InvalidateTenantModuleFeatureCache(scope.ServiceProvider, changedTenantModuleFeatures);

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
                request.Metadata?.TenantId,
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
                request.Metadata?.TenantId,
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
                request.Metadata?.TenantId,
                request.Changes.Count,
                string.Join(", ", request.Changes.Select(static change => change.EntityTypeName).Distinct()));
            return MemoryPack.MemoryPackSerializer.Serialize(
                DataContextResult.Failure("The requested data change could not be completed.", 500));
        }
    }

    private static bool ValidateTenantMetadata(
        object entity,
        RequestMetadata? metadata,
        Type entityType,
        out string error)
    {
        error = string.Empty;

        if (entity is not IHasTenantId tenantEntity || IsControlPlaneTenantRecord(entityType))
            return true;

        if (metadata?.TenantId is not { } metadataTenantId || metadataTenantId == Guid.Empty)
        {
            error = $"Entity '{entityType.Name}' requires tenant metadata for remote DataContext mutation.";
            return false;
        }

        if (tenantEntity.TenantId == Guid.Empty)
        {
            tenantEntity.TenantId = metadataTenantId;
            return true;
        }

        if (tenantEntity.TenantId == metadataTenantId)
            return true;

        error = $"Entity '{entityType.Name}' TenantId does not match request tenant metadata.";
        return false;
    }

    private static bool IsControlPlaneTenantRecord(Type entityType) =>
        entityType == typeof(Tenant) ||
        entityType == typeof(TenantModuleFeature);

    private static bool HasRequiredQueryTenantMetadata(Type entityType, RequestMetadata? metadata) =>
        !RequiresTenantMetadata(entityType) ||
        metadata?.TenantId is { } tenantId && tenantId != Guid.Empty;

    private static bool RequiresTenantMetadata(Type entityType) =>
        typeof(IHasTenantId).IsAssignableFrom(entityType) &&
        !IsControlPlaneTenantRecord(entityType);

    private async Task<string?> ValidateTrustedDataContextAsync(
        RequestMetadata? metadata,
        IReadOnlyCollection<string> scopes,
        bool requireTenant,
        CancellationToken ct)
    {
        if (serviceInvocationResolver is null)
            return null;

        var expectedAudience = configuration?["BoltConfiguration:ClientName"];
        if (string.IsNullOrWhiteSpace(expectedAudience))
            return "BoltConfiguration:ClientName is required for remote DataContext trust validation.";

        var result = await serviceInvocationResolver.ResolveAsync(
            metadata,
            expectedAudience,
            scopes,
            requireTenant: requireTenant,
            ct: ct);

        return result.IsSuccess ? null : result.Error;
    }

    private static async Task<string?> ValidateCredentialCapabilityAsync(
        IServiceProvider scopedServices,
        Type entityType,
        RequestMetadata? metadata,
        string capabilityKey,
        CancellationToken ct)
    {
        if (metadata?.CredentialId is not { } credentialId || credentialId == Guid.Empty)
            return null;

        var feature = ResolveIdentityFeature(entityType);
        if (feature is null)
            return null;

        var actorTenantId = metadata.ActorTenantId ?? metadata.TenantId;
        if (actorTenantId is null || actorTenantId == Guid.Empty)
            return "Credential capability validation requires an actor tenant context.";

        var capabilityService = scopedServices.GetService<ITenantCredentialCapabilityService>();
        if (capabilityService is null)
            return "Credential capability validation is unavailable.";

        var result = await capabilityService.EnsureAllowedAsync(
            actorTenantId.Value,
            credentialId,
            feature.Value.ModuleKey,
            feature.Value.SubFeatureKey,
            capabilityKey,
            ct);

        return result.IsSuccess ? null : "Credential capability is not allowed for this operation.";
    }

    private static async Task<string?> ValidateTargetFeatureAsync(
        IServiceProvider scopedServices,
        Type entityType,
        RequestMetadata? metadata,
        CancellationToken ct)
    {
        var feature = ResolveIdentityFeature(entityType);
        if (feature is null || metadata?.TenantId is not { } targetTenantId || targetTenantId == Guid.Empty)
            return null;

        var featureService = scopedServices.GetService<ITenantModuleFeatureService>();
        if (featureService is null)
            return "Tenant feature validation is unavailable.";

        var result = await featureService.EnsureEnabledAsync(
            targetTenantId,
            feature.Value.ModuleKey,
            feature.Value.SubFeatureKey,
            ct);
        return result.IsSuccess ? null : "The requested tenant feature is not enabled.";
    }

    private static async Task<string?> ValidateRemoteMutationAsync(
        IServiceProvider scopedServices,
        Type entityType,
        object entity,
        CancellationToken ct)
    {
        if (ResolveIdentityFeature(entityType) is null)
            return null;

        var validator = scopedServices
            .GetServices<IRemoteDataContextEntityValidator>()
            .SingleOrDefault(candidate => candidate.EntityType == entityType);
        if (validator is null)
            return $"Entity '{entityType.Name}' is not configured for validated remote mutation.";

        var errors = await validator.ValidateAsync(entity, ct);
        return errors.Count == 0
            ? null
            : $"Entity validation failed: {string.Join("; ", errors)}";
    }

    private static (string ModuleKey, string SubFeatureKey)? ResolveIdentityFeature(Type entityType)
    {
        var featureKey = entityType == typeof(IdentityInformation)
            ? TenantModuleFeatureKeys.IdentityUsers
            : entityType == typeof(IdentityCredential)
                ? TenantModuleFeatureKeys.IdentityCredentials
                : entityType == typeof(IdentityRole)
                    || entityType == typeof(IdentityRoleType)
                    || entityType == typeof(IdentityRoleTypeGroup)
                    || entityType == typeof(IdentityRoleTypeFeaturePermission)
                    || entityType == typeof(IdentityRoleFeaturePermissionOverride)
                        ? TenantModuleFeatureKeys.IdentityRoles
                : entityType == typeof(Tenant)
                    || entityType == typeof(TenantModuleFeature)
                    || entityType == typeof(TenantAuthorizationPolicy)
                        ? TenantModuleFeatureKeys.IdentityTenants
                : entityType == typeof(Session) || entityType == typeof(SessionType)
                    ? TenantModuleFeatureKeys.IdentitySessions
                : entityType == typeof(IdentityVerification)
                    || entityType == typeof(IdentityVerificationType)
                        ? TenantModuleFeatureKeys.IdentityVerifications
                : entityType == typeof(IdentityContact)
                    || entityType == typeof(IdentityContactType)
                    || entityType == typeof(IdentityContactGroup)
                        ? TenantModuleFeatureKeys.IdentityContacts
                : entityType == typeof(IdentityAddress)
                    || entityType == typeof(IdentityAddressType)
                    || entityType == typeof(AddressCountry)
                    || entityType == typeof(AddressRegion)
                    || entityType == typeof(AddressProvince)
                    || entityType == typeof(AddressCity)
                    || entityType == typeof(AddressBarangay)
                        ? TenantModuleFeatureKeys.IdentityAddresses
                : entityType == typeof(AuthorizationLog)
                    ? TenantModuleFeatureKeys.IdentityAuthLogs
                : entityType == typeof(IdentityFavorite)
                    || entityType == typeof(RegistryConfiguration)
                    || entityType == typeof(RegistryConfigurationGroup)
                    || entityType == typeof(RegistryFavoriteType)
                        ? TenantModuleFeatureKeys.Identity
                : null;

        if (featureKey is null)
            return null;

        var normalized = TenantModuleFeatureKeys.Normalize(featureKey);
        return normalized;
    }

    private static string ResolveMutationCapability(ChangeOperation operation) => operation switch
    {
        ChangeOperation.Add => IdentityAuthorizationConstants.Create,
        ChangeOperation.Update => IdentityAuthorizationConstants.Update,
        ChangeOperation.Remove => IdentityAuthorizationConstants.Delete,
        _ => IdentityAuthorizationConstants.Manage
    };

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

    private static byte[] SerializeError(string message)
    {
        var result = DataContextResult.Failure(message);
        return MemoryPack.MemoryPackSerializer.Serialize(result);
    }
}
