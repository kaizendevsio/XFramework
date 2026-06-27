using System.Globalization;
using Communications.Domain.Shared;
using Communications.Domain.Shared.Contracts.Requests.Settings;
using Communications.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;
using XFramework.Core.Services;
using XFramework.Domain.Shared.DataContext;

namespace Communications.Api.Services;

public sealed class CommunicationsSettingsService(
    IDataContext dataContext,
    ITenantResolver tenantResolver,
    ICommunicationsRequestContextResolver requestContextResolver,
    ICommunicationsPolicyService policyService) : ICommunicationsSettingsService
{
    public async Task<Result<CommunicationsSettingsResponse>> GetSettingsAsync(
        GetCommunicationsSettingsRequest request,
        CancellationToken ct = default)
    {
        var adminContext = requestContextResolver.ResolveAdmin(request.Metadata);
        if (!adminContext.IsSuccess)
        {
            return Result<CommunicationsSettingsResponse>.Failure(
                adminContext.Message ?? "Communications settings require an admin context",
                adminContext.StatusCode);
        }

        var tenantResult = await ResolveTenantIdAsync(adminContext.Data!.TenantId);
        if (!tenantResult.IsSuccess)
        {
            return Result<CommunicationsSettingsResponse>.Failure(
                tenantResult.Message ?? "Tenant could not be resolved",
                tenantResult.StatusCode);
        }

        var response = await BuildResponseAsync(tenantResult.Data, ct);
        return Result<CommunicationsSettingsResponse>.Success(response);
    }

    public async Task<Result<CommunicationsSettingsResponse>> UpdateSettingsAsync(
        UpdateCommunicationsSettingsRequest request,
        CancellationToken ct = default)
    {
        var adminContext = requestContextResolver.ResolveAdmin(request.Metadata);
        if (!adminContext.IsSuccess)
        {
            return Result<CommunicationsSettingsResponse>.Failure(
                adminContext.Message ?? "Communications settings require an admin context",
                adminContext.StatusCode);
        }

        var tenantResult = await ResolveTenantIdAsync(adminContext.Data!.TenantId);
        if (!tenantResult.IsSuccess)
        {
            return Result<CommunicationsSettingsResponse>.Failure(
                tenantResult.Message ?? "Tenant could not be resolved",
                tenantResult.StatusCode);
        }

        var tenantId = tenantResult.Data;
        var validation = ValidateRequest(request);
        if (validation.Count > 0)
        {
            return Result<CommunicationsSettingsResponse>.ValidationError(validation);
        }

        var now = DateTime.UtcNow;
        var groups = await LoadGroupsAsync(tenantId, ct);
        var configs = await LoadConfigsAsync(tenantId, ct);

        foreach (var valueRequest in request.Values)
        {
            var definition = CommunicationsSettingsCatalog.Find(valueRequest.GroupName, valueRequest.Key)!;
            var value = NormalizeValue(definition, valueRequest.Value).Value;
            var group = EnsureGroup(groups, definition, tenantId, now);
            var config = FindConfig(configs, groups, definition);

            if (config is null)
            {
                config = new RegistryConfiguration
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    Key = definition.Key,
                    Value = value,
                    Unit = definition.Unit,
                    GroupId = group.Id,
                    IsEnabled = true,
                    IsDeleted = false,
                    CreatedAt = now,
                    ModifiedAt = now,
                    ConcurrencyStamp = Guid.NewGuid()
                };

                dataContext.Add(config);
                configs.Add(config);
            }
            else
            {
                config.Key = definition.Key;
                config.Value = value;
                config.Unit = definition.Unit;
                config.GroupId = group.Id;
                config.IsEnabled = true;
                config.IsDeleted = false;
                config.ModifiedAt = now;
                config.ConcurrencyStamp = Guid.NewGuid();
                dataContext.Update(config);
            }
        }

        var saveResult = await dataContext.SaveChangesAsync(ct);
        if (!saveResult.IsSuccess)
        {
            return Result<CommunicationsSettingsResponse>.Failure(
                saveResult.Message ?? "Communications settings could not be saved",
                saveResult.StatusCode);
        }

        policyService.Invalidate(tenantId);
        var response = await BuildResponseAsync(tenantId, ct);
        return Result<CommunicationsSettingsResponse>.Success(response, "Communications settings saved.");
    }

    private async Task<Result<Guid>> ResolveTenantIdAsync(Guid? tenantId)
    {
        try
        {
            var tenant = await tenantResolver.GetTenant(tenantId);
            return Result<Guid>.Success(tenant.Id);
        }
        catch (ArgumentNullException)
        {
            return Result<Guid>.Failure("Tenant id is required.", 400);
        }
        catch (InvalidOperationException)
        {
            return Result<Guid>.NotFound("Tenant could not be found.");
        }
    }

    private async Task<CommunicationsSettingsResponse> BuildResponseAsync(
        Guid tenantId,
        CancellationToken ct)
    {
        var groups = await LoadGroupsAsync(tenantId, ct);
        var configs = await LoadConfigsAsync(tenantId, ct);

        var responseGroups = CommunicationsSettingsCatalog.Definitions
            .GroupBy(x => new { x.SectionKey, x.GroupName })
            .Select(grouping => new CommunicationsSettingGroupResponse
            {
                SectionKey = grouping.Key.SectionKey,
                GroupName = grouping.Key.GroupName,
                Title = GetGroupTitle(grouping.Key.GroupName),
                Description = GetGroupDescription(grouping.Key.GroupName),
                Settings = grouping
                    .Select(definition => BuildValueResponse(configs, groups, definition))
                    .ToList()
            })
            .ToList();

        return new CommunicationsSettingsResponse
        {
            TenantId = tenantId,
            Groups = responseGroups,
            LoadedAtUtc = DateTime.UtcNow
        };
    }

    private async Task<List<RegistryConfigurationGroup>> LoadGroupsAsync(
        Guid tenantId,
        CancellationToken ct) =>
        await dataContext.Query<RegistryConfigurationGroup>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .OrderBy(x => x.Name)
            .Take(250)
            .ToListAsync(ct);

    private async Task<List<RegistryConfiguration>> LoadConfigsAsync(
        Guid tenantId,
        CancellationToken ct) =>
        await dataContext.Query<RegistryConfiguration>()
            .IgnoreQueryFilters()
            .NoCache()
            .Include(x => x.Group)
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .OrderBy(x => x.Key)
            .Take(750)
            .ToListAsync(ct);

    private static CommunicationsSettingValueResponse BuildValueResponse(
        IReadOnlyCollection<RegistryConfiguration> configs,
        IReadOnlyCollection<RegistryConfigurationGroup> groups,
        CommunicationsSettingDefinition definition)
    {
        var config = FindConfig(configs, groups, definition);

        return new CommunicationsSettingValueResponse
        {
            SectionKey = definition.SectionKey,
            GroupName = definition.GroupName,
            Key = definition.Key,
            Label = definition.Label,
            Description = definition.Description,
            Value = config?.Value ?? definition.DefaultValue,
            DefaultValue = definition.DefaultValue,
            Unit = config?.Unit ?? definition.Unit,
            Source = config is null ? CommunicationsSettingSources.Default : CommunicationsSettingSources.Stored,
            ValueKind = definition.ValueKind,
            Options = definition.Options?.ToList() ?? [],
            LastUpdated = config?.ModifiedAt ?? config?.CreatedAt
        };
    }

    private static RegistryConfiguration? FindConfig(
        IEnumerable<RegistryConfiguration> configs,
        IReadOnlyCollection<RegistryConfigurationGroup> groups,
        CommunicationsSettingDefinition definition)
    {
        var exact = configs.FirstOrDefault(config =>
            string.Equals(config.Key, definition.Key, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(GetConfigGroupName(config, groups), definition.GroupName, StringComparison.OrdinalIgnoreCase));

        if (exact is not null || !definition.MatchFirstConfigInGroup)
        {
            return exact;
        }

        return configs.FirstOrDefault(config =>
            string.Equals(GetConfigGroupName(config, groups), definition.GroupName, StringComparison.OrdinalIgnoreCase));
    }

    private static string GetConfigGroupName(
        RegistryConfiguration config,
        IEnumerable<RegistryConfigurationGroup> groups)
    {
        if (!string.IsNullOrWhiteSpace(config.Group?.Name))
        {
            return config.Group.Name;
        }

        return groups.FirstOrDefault(group => group.Id == config.GroupId)?.Name ?? string.Empty;
    }

    private RegistryConfigurationGroup EnsureGroup(
        ICollection<RegistryConfigurationGroup> groups,
        CommunicationsSettingDefinition definition,
        Guid tenantId,
        DateTime now)
    {
        var group = groups.FirstOrDefault(x =>
            x.SystemReferenceId == definition.GroupSystemReferenceId ||
            string.Equals(x.Name, definition.GroupName, StringComparison.OrdinalIgnoreCase));

        if (group is not null)
        {
            return group;
        }

        group = new RegistryConfigurationGroup
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = definition.GroupName,
            Description = GetGroupDescription(definition.GroupName),
            SystemReferenceId = definition.GroupSystemReferenceId,
            IsEnabled = true,
            IsDeleted = false,
            CreatedAt = now,
            ModifiedAt = now,
            ConcurrencyStamp = Guid.NewGuid()
        };

        dataContext.Add(group);
        groups.Add(group);
        return group;
    }

    private static Dictionary<string, string[]> ValidateRequest(UpdateCommunicationsSettingsRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < request.Values.Count; index++)
        {
            var item = request.Values[index];
            var fieldName = $"Values[{index}]";

            if (string.IsNullOrWhiteSpace(item.GroupName))
            {
                errors[$"{fieldName}.GroupName"] = ["Setting group is required."];
                continue;
            }

            if (string.IsNullOrWhiteSpace(item.Key))
            {
                errors[$"{fieldName}.Key"] = ["Setting key is required."];
                continue;
            }

            var identifier = $"{item.GroupName}:{item.Key}";
            if (!seen.Add(identifier))
            {
                errors[fieldName] = ["Duplicate setting value."];
                continue;
            }

            var definition = CommunicationsSettingsCatalog.Find(item.GroupName, item.Key);
            if (definition is null)
            {
                errors[fieldName] = [$"Unknown Communications setting '{identifier}'."];
                continue;
            }

            var normalized = NormalizeValue(definition, item.Value);
            if (!normalized.IsValid)
            {
                errors[identifier] = normalized.Errors;
            }
        }

        return errors;
    }

    private static NormalizedSettingValue NormalizeValue(
        CommunicationsSettingDefinition definition,
        string? rawValue)
    {
        var value = rawValue?.Trim() ?? string.Empty;

        return definition.ValueKind switch
        {
            CommunicationsSettingValueKind.Boolean => NormalizeBoolean(value),
            CommunicationsSettingValueKind.Number => NormalizeNumber(value),
            CommunicationsSettingValueKind.Option => NormalizeOption(definition, value),
            CommunicationsSettingValueKind.Csv => NormalizeCsv(value),
            CommunicationsSettingValueKind.Template => NormalizeTemplate(value),
            _ => NormalizeText(definition, value)
        };
    }

    private static NormalizedSettingValue NormalizeBoolean(string value)
    {
        if (!bool.TryParse(value, out var parsed))
        {
            return NormalizedSettingValue.Invalid("Value must be true or false.");
        }

        return NormalizedSettingValue.Valid(parsed.ToString().ToLowerInvariant());
    }

    private static NormalizedSettingValue NormalizeNumber(string value)
    {
        if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) || parsed < 0)
        {
            return NormalizedSettingValue.Invalid("Value must be a non-negative whole number.");
        }

        return NormalizedSettingValue.Valid(parsed.ToString(CultureInfo.InvariantCulture));
    }

    private static NormalizedSettingValue NormalizeOption(
        CommunicationsSettingDefinition definition,
        string value)
    {
        var option = definition.Options?.FirstOrDefault(x =>
            string.Equals(x, value, StringComparison.OrdinalIgnoreCase));

        if (option is null)
        {
            return NormalizedSettingValue.Invalid("Value must match one of the supported options.");
        }

        return NormalizedSettingValue.Valid(option);
    }

    private static NormalizedSettingValue NormalizeCsv(string value)
    {
        if (value.Length > 2000)
        {
            return NormalizedSettingValue.Invalid("CSV value cannot exceed 2,000 characters.");
        }

        if (value.Any(character => char.IsControl(character) && character is not '\t'))
        {
            return NormalizedSettingValue.Invalid("CSV value cannot contain control characters or line breaks.");
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            return NormalizedSettingValue.Valid(string.Empty);
        }

        var parts = value.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Any(string.IsNullOrWhiteSpace))
        {
            return NormalizedSettingValue.Invalid("CSV value cannot contain empty entries.");
        }

        return NormalizedSettingValue.Valid(string.Join(',', parts));
    }

    private static NormalizedSettingValue NormalizeTemplate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return NormalizedSettingValue.Invalid("Template value is required.");
        }

        if (value.Length > 4000)
        {
            return NormalizedSettingValue.Invalid("Template value cannot exceed 4,000 characters.");
        }

        return NormalizedSettingValue.Valid(value);
    }

    private static NormalizedSettingValue NormalizeText(
        CommunicationsSettingDefinition definition,
        string value)
    {
        if (value.Length > 1000)
        {
            return NormalizedSettingValue.Invalid("Value cannot exceed 1,000 characters.");
        }

        if (string.Equals(definition.Unit, "guid", StringComparison.OrdinalIgnoreCase) &&
            value.Length > 0 &&
            !Guid.TryParse(value, out _))
        {
            return NormalizedSettingValue.Invalid("Value must be a valid GUID.");
        }

        return NormalizedSettingValue.Valid(value);
    }

    private static string GetGroupTitle(string groupName) =>
        groupName switch
        {
            "Communications.Chat" => "Chat Controls",
            "Communications.Policy" => "Policy Controls",
            "Communications.Transport" => "Transport Defaults",
            "CommunicationsService_Otp" => "OTP Template",
            "CommunicationsService_PasswordReset" => "Password Reset Template",
            _ => groupName
        };

    private static string GetGroupDescription(string groupName) =>
        groupName switch
        {
            "Communications.Chat" => "Thread, direct-message, read-state, typing, and presence defaults.",
            "Communications.Policy" => "Attachment, retention, rate-limit, and moderation settings.",
            "Communications.Transport" => "Legacy transport and notification fallback settings.",
            "CommunicationsService_Otp" => "Identity one-time password template used by Communications.",
            "CommunicationsService_PasswordReset" => "Identity password reset template used by Communications.",
            _ => "Communications tenant settings."
        };

    private readonly record struct NormalizedSettingValue(
        bool IsValid,
        string Value,
        string[] Errors)
    {
        public static NormalizedSettingValue Valid(string value) => new(true, value, []);

        public static NormalizedSettingValue Invalid(string error) => new(false, string.Empty, [error]);
    }
}
