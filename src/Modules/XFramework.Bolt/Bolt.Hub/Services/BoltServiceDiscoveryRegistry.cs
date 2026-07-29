using System.Security.Cryptography;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Bolt.Domain.Shared.Contracts.ServiceDiscovery;
using Bolt.Server;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;
using XFramework.Domain.Shared.ServiceIdentity;

namespace Bolt.Hub.Services;

public sealed class BoltServiceDiscoveryRegistry(
    DbContext db,
    IBoltServicePresenceTracker presenceTracker,
    IMemoryCache cache) : IBoltServiceDiscoveryRegistry
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan OfflineRetention = TimeSpan.FromDays(30);
    private const int MaximumRegistryRecords = 1000;
    private const string OnlineSnapshotKey = "bolt-discovery:online";
    private const string CompleteSnapshotKey = "bolt-discovery:complete";
    private static readonly TimeSpan SnapshotLifetime = TimeSpan.FromSeconds(30);

    public async Task ResetPresenceAsync(CancellationToken ct)
    {
        presenceTracker.Clear();

        await RetireStaleAsync(ct);

        var now = DateTime.UtcNow;
        var records = await db.Set<BoltServiceManifestRecord>()
            .AsTracking()
            .Where(record => record.IsConnected || record.ConnectionCount > 0)
            .ToListAsync(ct);

        foreach (var record in records)
        {
            record.IsConnected = false;
            record.ConnectionCount = 0;
            record.LastSeenAt = now;
            record.LastDisconnectedAt = now;
        }

        await db.SaveChangesAsync(ct);
        InvalidateSnapshots();
    }

    public async Task RetireStaleAsync(CancellationToken ct)
    {
        var staleCutoff = DateTime.UtcNow - OfflineRetention;
        var staleRecords = db.Set<BoltServiceManifestRecord>()
            .Where(record => !record.IsConnected && record.LastSeenAt < staleCutoff);
        if (db.Database.IsRelational())
        {
            if (await staleRecords.ExecuteDeleteAsync(ct) > 0)
                InvalidateSnapshots();
            return;
        }

        var records = await staleRecords.ToListAsync(ct);
        if (records.Count == 0)
            return;
        db.RemoveRange(records);
        await db.SaveChangesAsync(ct);
        InvalidateSnapshots();
    }

    public async Task<BoltServiceManifestAdvertisementResponse> AdvertiseAsync(
        BoltRequestContext context,
        BoltServiceManifest manifest,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(context.ClientId))
        {
            return new BoltServiceManifestAdvertisementResponse
            {
                Accepted = false,
                Message = "Registered Bolt client id is required before advertising a manifest."
            };
        }

        var authorization = ValidateManifestAdvertisement(context, manifest);
        if (!authorization.Accepted)
        {
            return authorization;
        }

        return await presenceTracker.UpdateAsync(
            context.ClientId,
            async connectionIds =>
            {
                if (!string.IsNullOrWhiteSpace(context.ConnectionId))
                {
                    connectionIds.Add(context.ConnectionId);
                }

                return await UpsertAdvertisedManifestAsync(
                    context.ClientId,
                    context,
                    manifest,
                    Math.Max(1, connectionIds.Count),
                    ct);
            },
            ct);
    }

    private async Task<BoltServiceManifestAdvertisementResponse> UpsertAdvertisedManifestAsync(
        string clientId,
        BoltRequestContext context,
        BoltServiceManifest manifest,
        int connectionCount,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var normalized = NormalizeManifest(context, manifest);
        var manifestJson = JsonSerializer.Serialize(normalized, JsonOptions);
        var manifestHash = ComputeSha256(manifestJson);
        var record = await db.Set<BoltServiceManifestRecord>()
            .AsTracking()
            .FirstOrDefaultAsync(x => x.ClientId == clientId, ct);

        if (record is null)
        {
            record = new BoltServiceManifestRecord
            {
                Id = Guid.NewGuid(),
                ClientId = clientId,
                CreatedAt = now
            };
            db.Add(record);
        }

        record.ClientName = context.ClientName ?? clientId;
        record.ServiceName = normalized.ServiceName;
        record.DisplayName = normalized.DisplayName;
        record.Version = normalized.Version;
        record.IsConnected = true;
        record.ConnectionCount = connectionCount;
        record.LastSeenAt = now;
        record.LastConnectedAt ??= now;

        if (!string.Equals(record.ManifestHash, manifestHash, StringComparison.Ordinal))
        {
            record.ManifestHash = manifestHash;
            record.ManifestJson = manifestJson;
        }

        await db.SaveChangesAsync(ct);
        InvalidateSnapshots();
        return new BoltServiceManifestAdvertisementResponse { Accepted = true, Message = "Accepted" };
    }

    public async Task MarkConnectedAsync(BoltClientConnectionEvent connectionEvent, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(connectionEvent.ClientId))
        {
            return;
        }

        await presenceTracker.UpdateAsync(
            connectionEvent.ClientId,
            async connectionIds =>
            {
                connectionIds.Add(connectionEvent.ConnectionId);
                await UpsertConnectedRecordAsync(connectionEvent, connectionIds.Count, ct);
                return true;
            },
            ct);
    }

    public async Task MarkDisconnectedAsync(BoltClientConnectionEvent connectionEvent, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(connectionEvent.ClientId))
        {
            return;
        }

        await presenceTracker.UpdateAsync(
            connectionEvent.ClientId,
            async connectionIds =>
            {
                connectionIds.Remove(connectionEvent.ConnectionId);
                await MarkDisconnectedRecordAsync(connectionEvent, connectionIds.Count, ct);
                return true;
            },
            ct);
    }

    private async Task UpsertConnectedRecordAsync(
        BoltClientConnectionEvent connectionEvent,
        int connectionCount,
        CancellationToken ct)
    {
        var now = connectionEvent.OccurredAt == default ? DateTime.UtcNow : connectionEvent.OccurredAt;
        var record = await db.Set<BoltServiceManifestRecord>()
            .AsTracking()
            .FirstOrDefaultAsync(x => x.ClientId == connectionEvent.ClientId, ct);

        if (record is null)
        {
            var manifest = NormalizeManifest(
                new BoltRequestContext(
                    connectionEvent.ConnectionId,
                    connectionEvent.ClientId,
                    connectionEvent.ClientName,
                    connectionEvent.ServiceHash,
                    connectionEvent.TransportType,
                    null),
                new BoltServiceManifest());
            var manifestJson = JsonSerializer.Serialize(manifest, JsonOptions);

            record = new BoltServiceManifestRecord
            {
                Id = Guid.NewGuid(),
                ClientId = connectionEvent.ClientId,
                ClientName = connectionEvent.ClientName ?? connectionEvent.ClientId,
                ServiceName = manifest.ServiceName,
                DisplayName = manifest.DisplayName,
                Version = manifest.Version,
                ManifestJson = manifestJson,
                ManifestHash = ComputeSha256(manifestJson),
                CreatedAt = now
            };
            db.Add(record);
        }

        record.ClientName = connectionEvent.ClientName ?? connectionEvent.ClientId;
        record.IsConnected = true;
        record.ConnectionCount = Math.Max(1, connectionCount);
        record.LastSeenAt = now;
        record.LastConnectedAt = now;

        await db.SaveChangesAsync(ct);
        InvalidateSnapshots();
    }

    private async Task MarkDisconnectedRecordAsync(
        BoltClientConnectionEvent connectionEvent,
        int connectionCount,
        CancellationToken ct)
    {
        var record = await db.Set<BoltServiceManifestRecord>()
            .AsTracking()
            .FirstOrDefaultAsync(x => x.ClientId == connectionEvent.ClientId, ct);

        if (record is null)
        {
            return;
        }

        var count = Math.Max(0, connectionCount);
        record.ConnectionCount = count;
        record.IsConnected = count > 0;
        record.LastSeenAt = DateTime.UtcNow;
        if (count == 0)
        {
            record.LastDisconnectedAt = connectionEvent.OccurredAt == default
                ? DateTime.UtcNow
                : connectionEvent.OccurredAt;
        }

        await db.SaveChangesAsync(ct);
        InvalidateSnapshots();
    }

    public async Task<BoltServiceRegistryResponse> GetServicesAsync(
        BoltServiceRegistryRequest request,
        CancellationToken ct)
    {
        var snapshot = await LoadSnapshotAsync(request.IncludeOffline, ct);
        var records = snapshot.Records;
        var index = snapshot.Index;
        var items = records
            .Select(record =>
            {
                var manifest = index.Manifests[record.ClientId];
                var dependencyStatuses = EvaluateDependencies(manifest.Dependencies, index);
                var status = GetStatus(record, dependencyStatuses);

                return new BoltServiceRegistryItem
                {
                    ClientId = record.ClientId,
                    ClientName = record.ClientName,
                    ServiceName = record.ServiceName,
                    DisplayName = record.DisplayName,
                    Version = record.Version,
                    Status = status,
                    ConnectionCount = record.ConnectionCount,
                    LastSeenAt = record.LastSeenAt,
                    LastConnectedAt = record.LastConnectedAt,
                    LastDisconnectedAt = record.LastDisconnectedAt,
                    Manifest = manifest,
                    DependencyStatuses = dependencyStatuses
                };
            })
            .OrderBy(item => item.DisplayName)
            .ThenBy(item => item.ClientId)
            .ToList();

        return new BoltServiceRegistryResponse { Services = items };
    }

    public async Task<BoltModuleRegistryResponse> GetModulesAsync(
        BoltModuleRegistryRequest request,
        CancellationToken ct)
    {
        var snapshot = await LoadSnapshotAsync(request.IncludeOffline, ct);
        var records = snapshot.Records;
        var index = snapshot.Index;
        var modules = new List<BoltModuleRegistryItem>();

        foreach (var record in records)
        {
            var manifest = index.Manifests[record.ClientId];
            var serviceDependencies = EvaluateDependencies(manifest.Dependencies, index);
            var serviceStatus = GetStatus(record, serviceDependencies);

            foreach (var module in manifest.Modules)
            {
                var moduleDependencies = serviceDependencies
                    .Concat(EvaluateDependencies(module.Dependencies, index))
                    .ToList();
                var moduleStatus = GetStatus(record, moduleDependencies);

                modules.Add(new BoltModuleRegistryItem
                {
                    ModuleKey = module.ModuleKey,
                    DisplayName = module.DisplayName,
                    Description = module.Description,
                    Version = module.Version ?? manifest.Version,
                    IconName = module.IconName,
                    ServiceName = manifest.ServiceName,
                    ClientId = record.ClientId,
                    ClientName = record.ClientName,
                    Status = serviceStatus == BoltRegistryStatus.Offline ? serviceStatus : moduleStatus,
                    DependencyStatuses = moduleDependencies,
                    Features = module.Features
                        .Select(feature => CreateFeatureItem(feature, record, index, moduleDependencies))
                        .ToList()
                });
            }
        }

        return new BoltModuleRegistryResponse
        {
            Modules = modules
                .OrderBy(module => module.DisplayName)
                .ThenBy(module => module.ModuleKey)
                .ToList()
        };
    }

    private async Task<DiscoverySnapshot> LoadSnapshotAsync(bool includeOffline, CancellationToken ct)
    {
        var key = includeOffline ? CompleteSnapshotKey : OnlineSnapshotKey;
        if (cache.TryGetValue(key, out DiscoverySnapshot? cached) && cached is not null)
            return cached;

        var records = await db.Set<BoltServiceManifestRecord>()
            .AsNoTracking()
            .Where(record => includeOffline ||
                (record.IsConnected && record.ConnectionCount > 0))
            .OrderBy(record => record.ServiceName)
            .Take(MaximumRegistryRecords)
            .ToListAsync(ct);
        var snapshot = new DiscoverySnapshot(records, CreateDiscoveryIndex(records));
        cache.Set(key, snapshot, SnapshotLifetime);
        return snapshot;
    }

    private void InvalidateSnapshots()
    {
        cache.Remove(OnlineSnapshotKey);
        cache.Remove(CompleteSnapshotKey);
    }

    private static BoltTenantModuleFeatureRegistryItem CreateFeatureItem(
        BoltTenantModuleFeatureManifest feature,
        BoltServiceManifestRecord owner,
        DiscoveryIndex index,
        IReadOnlyList<BoltDependencyStatus> inheritedDependencies)
    {
        var dependencyStatuses = inheritedDependencies
            .Concat(EvaluateDependencies(feature.Dependencies, index))
            .ToList();

        return new BoltTenantModuleFeatureRegistryItem
        {
            Key = feature.Key ?? CombineKey(feature.ModuleKey, feature.SubFeatureKey),
            ModuleKey = feature.ModuleKey,
            SubFeatureKey = feature.SubFeatureKey,
            DisplayName = feature.DisplayName,
            Description = feature.Description,
            IconName = feature.IconName,
            DefaultEnabled = feature.DefaultEnabled,
            Dependencies = feature.Dependencies,
            DependencyStatuses = dependencyStatuses,
            Status = GetStatus(owner, dependencyStatuses)
        };
    }

    private static BoltRegistryStatus GetStatus(
        BoltServiceManifestRecord record,
        IReadOnlyCollection<BoltDependencyStatus> dependencyStatuses)
    {
        if (!IsOnline(record))
        {
            return BoltRegistryStatus.Offline;
        }

        return dependencyStatuses.Any(status => status.Requirement.Required && !status.IsSatisfied)
            ? BoltRegistryStatus.Degraded
            : BoltRegistryStatus.Online;
    }

    private static List<BoltDependencyStatus> EvaluateDependencies(
        IEnumerable<BoltDependencyRequirement> requirements,
        DiscoveryIndex index) =>
        requirements
            .Select(requirement => EvaluateDependency(
                requirement,
                index.ServiceKeys,
                index.ModuleKeys,
                index.FeatureKeys))
            .ToList();

    private static DiscoveryIndex CreateDiscoveryIndex(IReadOnlyList<BoltServiceManifestRecord> records)
    {
        var manifests = records.ToDictionary(record => record.ClientId, DeserializeManifest, StringComparer.Ordinal);
        var onlineRecords = records.Where(IsOnline).ToList();
        var serviceKeys = onlineRecords
            .SelectMany(record => new[] { record.ClientId, record.ClientName, record.ServiceName })
            .Where(static key => !string.IsNullOrWhiteSpace(key))
            .Select(NormalizeLookupKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var onlineManifests = onlineRecords.Select(record => manifests[record.ClientId]).ToList();
        var moduleKeys = onlineManifests
            .SelectMany(static manifest => manifest.Modules)
            .Select(module => NormalizeLookupKey(module.ModuleKey))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var featureKeys = onlineManifests
            .SelectMany(static manifest => manifest.Modules)
            .SelectMany(static module => module.Features)
            .Select(feature => NormalizeLookupKey(feature.Key ?? CombineKey(feature.ModuleKey, feature.SubFeatureKey)))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return new DiscoveryIndex(manifests, serviceKeys, moduleKeys, featureKeys);
    }

    private static bool IsOnline(BoltServiceManifestRecord record) =>
        record.IsConnected &&
        record.ConnectionCount > 0;

    private sealed record DiscoveryIndex(
        IReadOnlyDictionary<string, BoltServiceManifest> Manifests,
        IReadOnlySet<string> ServiceKeys,
        IReadOnlySet<string> ModuleKeys,
        IReadOnlySet<string> FeatureKeys);

    private sealed record DiscoverySnapshot(
        IReadOnlyList<BoltServiceManifestRecord> Records,
        DiscoveryIndex Index);

    private static BoltDependencyStatus EvaluateDependency(
        BoltDependencyRequirement requirement,
        IReadOnlySet<string> serviceKeys,
        IReadOnlySet<string> moduleKeys,
        IReadOnlySet<string> featureKeys)
    {
        var key = NormalizeLookupKey(requirement.Key);
        var satisfied = requirement.Kind switch
        {
            BoltDependencyKind.Service => serviceKeys.Contains(key),
            BoltDependencyKind.Module => moduleKeys.Contains(key),
            BoltDependencyKind.TenantFeature => featureKeys.Contains(key),
            _ => false
        };

        var displayName = string.IsNullOrWhiteSpace(requirement.DisplayName)
            ? requirement.Key
            : requirement.DisplayName;

        return new BoltDependencyStatus
        {
            Requirement = requirement,
            IsSatisfied = satisfied,
            MatchedKey = satisfied ? requirement.Key : null,
            Message = satisfied
                ? $"{displayName} is available."
                : $"{displayName} is not available."
        };
    }

    private static BoltServiceManifest NormalizeManifest(BoltRequestContext context, BoltServiceManifest manifest)
    {
        manifest.ServiceName = FirstNonEmpty(manifest.ServiceName, context.ClientName, context.ClientId, "unknown");
        manifest.DisplayName = FirstNonEmpty(manifest.DisplayName, manifest.ServiceName);
        manifest.Description ??= string.Empty;
        manifest.Modules ??= [];
        manifest.Dependencies ??= [];
        manifest.Metadata ??= [];

        foreach (var module in manifest.Modules)
        {
            module.ModuleKey = NormalizeLookupKey(module.ModuleKey);
            module.DisplayName = FirstNonEmpty(module.DisplayName, module.ModuleKey);
            module.Description ??= string.Empty;
            module.IconName = FirstNonEmpty(module.IconName, "box");
            module.Features ??= [];
            module.Dependencies ??= [];

            foreach (var feature in module.Features)
            {
                NormalizeFeature(module, feature);
            }
        }

        return manifest;
    }

    private static BoltServiceManifestAdvertisementResponse ValidateManifestAdvertisement(
        BoltRequestContext context,
        BoltServiceManifest manifest)
    {
        var user = context.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return Reject("Authenticated service identity is required before advertising a manifest.");
        }

        if (!HasBoltServiceScope(user))
        {
            return Reject("Service identity must include the bolt.service scope before advertising a manifest.");
        }

        var serviceName = ResolveServiceName(user);
        if (string.IsNullOrWhiteSpace(serviceName))
        {
            return Reject("Service identity claim is required before advertising a manifest.");
        }

        if (!string.Equals(context.ClientName, serviceName, StringComparison.Ordinal))
        {
            return Reject("Registered Bolt client name must match the authenticated service identity.");
        }

        if (!string.Equals(context.ClientId, ComputeSha256(serviceName), StringComparison.OrdinalIgnoreCase))
        {
            return Reject("Registered Bolt client id must match the authenticated service identity.");
        }

        if (!string.IsNullOrWhiteSpace(manifest.ServiceName) &&
            !string.Equals(manifest.ServiceName, serviceName, StringComparison.Ordinal))
        {
            return Reject("Manifest service name must match the authenticated service identity.");
        }

        manifest.ServiceName = serviceName;
        return new BoltServiceManifestAdvertisementResponse { Accepted = true, Message = "Accepted" };
    }

    private static bool HasBoltServiceScope(ClaimsPrincipal user) =>
        user.Claims
            .Where(static claim => claim.Type is "scope" or "scp")
            .SelectMany(static claim => claim.Value.Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Any(static scope => string.Equals(scope, XFrameworkServiceScopes.BoltService, StringComparison.OrdinalIgnoreCase));

    private static string? ResolveServiceName(ClaimsPrincipal user) =>
        user.FindFirstValue("client_id") ??
        user.FindFirstValue("service") ??
        user.FindFirstValue("azp");

    private static BoltServiceManifestAdvertisementResponse Reject(string message) =>
        new()
        {
            Accepted = false,
            Message = message
        };

    private static void NormalizeFeature(BoltModuleManifest module, BoltTenantModuleFeatureManifest feature)
    {
        if (string.IsNullOrWhiteSpace(feature.ModuleKey))
        {
            feature.ModuleKey = module.ModuleKey;
        }

        if (!string.IsNullOrWhiteSpace(feature.Key))
        {
            var split = SplitCombinedKey(feature.Key);
            if (string.IsNullOrWhiteSpace(feature.ModuleKey))
            {
                feature.ModuleKey = split.ModuleKey;
            }

            if (string.IsNullOrWhiteSpace(feature.SubFeatureKey))
            {
                feature.SubFeatureKey = split.SubFeatureKey;
            }
        }

        feature.ModuleKey = NormalizeLookupKey(feature.ModuleKey);
        feature.SubFeatureKey = NormalizeLookupKey(feature.SubFeatureKey);
        feature.Key = CombineKey(feature.ModuleKey, feature.SubFeatureKey);
        feature.DisplayName = FirstNonEmpty(feature.DisplayName, feature.Key);
        feature.Description ??= string.Empty;
        feature.IconName = FirstNonEmpty(feature.IconName, module.IconName, "box");
        feature.Dependencies ??= [];
    }

    private static BoltServiceManifest DeserializeManifest(BoltServiceManifestRecord record)
    {
        try
        {
            return JsonSerializer.Deserialize<BoltServiceManifest>(record.ManifestJson, JsonOptions)
                ?? new BoltServiceManifest
                {
                    ServiceName = record.ServiceName,
                    DisplayName = record.DisplayName,
                    Version = record.Version
                };
        }
        catch (JsonException ex)
        {
            return new BoltServiceManifest
            {
                ServiceName = record.ServiceName,
                DisplayName = record.DisplayName,
                Version = record.Version,
                Metadata = { ["manifestError"] = ex.Message }
            };
        }
    }

    private static string ComputeSha256(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    private static string NormalizeLookupKey(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant();

    private static (string ModuleKey, string SubFeatureKey) SplitCombinedKey(string key)
    {
        var normalized = NormalizeLookupKey(key);
        var separatorIndex = normalized.IndexOf('.', StringComparison.Ordinal);
        return separatorIndex > 0 && separatorIndex < normalized.Length - 1
            ? (normalized[..separatorIndex], normalized[(separatorIndex + 1)..])
            : (normalized, string.Empty);
    }

    private static string CombineKey(string moduleKey, string? subFeatureKey = null)
    {
        var normalizedModule = NormalizeLookupKey(moduleKey);
        var normalizedSubFeature = NormalizeLookupKey(subFeatureKey);
        return string.IsNullOrWhiteSpace(normalizedSubFeature)
            ? normalizedModule
            : $"{normalizedModule}.{normalizedSubFeature}";
    }
}
