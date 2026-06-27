using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using IdentityServer.Domain.Shared.Contracts;
using Messaging.Domain.Shared;
using Messaging.Domain.Shared.Contracts.Requests.Templates;
using Messaging.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;
using XFramework.Core.Services;
using XFramework.Domain.Shared.DataContext;

namespace Messaging.Api.Services;

public sealed class MessagingTemplateService(
    IDataContext dataContext,
    ITenantResolver tenantResolver,
    IMessagingRequestContextResolver requestContextResolver,
    ILogger<MessagingTemplateService> logger) : IMessagingTemplateService
{
    private sealed record TemplateAccessContext(Guid TenantId, Guid? CredentialId, bool IsAdmin);

    private const int MaxKeyLength = 128;
    private const int MaxNameLength = 160;
    private const int MaxDescriptionLength = 1000;
    private const int MaxSubjectLength = 500;
    private const int MaxBodyLength = 4000;
    private static readonly Regex TokenRegex = new(@"\|([A-Za-z0-9_.-]+)\|", RegexOptions.Compiled);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<Result<GetMessageTemplatesResponse>> GetTemplatesAsync(
        GetMessageTemplatesRequest request,
        CancellationToken ct = default)
    {
        var accessResult = await ResolveTemplateAccessAsync(request.Metadata);
        if (!accessResult.IsSuccess)
            return Result<GetMessageTemplatesResponse>.Failure(accessResult.Message ?? "Tenant could not be resolved", accessResult.StatusCode);

        var access = accessResult.Data!;
        var tenantId = access.TenantId;
        await EnsureTenantTemplatesAsync(tenantId, ct);

        var pageIndex = Math.Max(request.PageIndex, 0);
        var pageSize = request.PageSize <= 0 ? 20 : Math.Min(request.PageSize, 500);
        var rows = await LoadTemplatesAsync(tenantId, includeInactive: access.IsAdmin && request.IncludeInactive, ct);

        if (!access.IsAdmin)
        {
            if (request.OwnerCredentialId is Guid requestedOwner &&
                requestedOwner != access.CredentialId)
            {
                return Result<GetMessageTemplatesResponse>.Forbidden("User templates can only be listed for the current credential.");
            }

            rows = rows
                .Where(template => CanUserAccessTemplate(template, access.CredentialId))
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(request.TemplateType))
        {
            rows = rows
                .Where(template => string.Equals(template.TemplateType, request.TemplateType, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (request.OwnerCredentialId is Guid ownerCredentialId)
        {
            rows = rows
                .Where(template => template.OwnerCredentialId == ownerCredentialId)
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            rows = rows
                .Where(template =>
                    template.Key.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    template.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    (template.Description?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (template.Body?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false))
                .ToList();
        }

        var total = rows.Count;
        var page = rows
            .OrderBy(template => TemplateTypeRank(template.TemplateType))
            .ThenBy(template => template.Key)
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .ToList();

        var ownerLabels = await LoadOwnerLabelsAsync(tenantId, page.Select(template => template.OwnerCredentialId), ct);
        return Result<GetMessageTemplatesResponse>.Success(new GetMessageTemplatesResponse
        {
            Items = page.Select(template => ToResponse(template, ownerLabels)).ToList(),
            PageIndex = pageIndex,
            PageSize = pageSize,
            TotalCount = total
        });
    }

    public async Task<Result<MessageTemplateResponse>> GetTemplateAsync(
        GetMessageTemplateRequest request,
        CancellationToken ct = default)
    {
        var accessResult = await ResolveTemplateAccessAsync(request.Metadata);
        if (!accessResult.IsSuccess)
            return Result<MessageTemplateResponse>.Failure(accessResult.Message ?? "Tenant could not be resolved", accessResult.StatusCode);

        var access = accessResult.Data!;
        var tenantId = access.TenantId;
        await EnsureTenantTemplatesAsync(tenantId, ct);

        var template = await FindTemplateByIdAsync(tenantId, request.TemplateId, includeInactive: access.IsAdmin, ct);
        if (template is null)
            return Result<MessageTemplateResponse>.NotFound("Message template not found.");

        if (!access.IsAdmin && !CanUserAccessTemplate(template, access.CredentialId))
            return Result<MessageTemplateResponse>.Forbidden("Message template is not available to this credential.");

        var ownerLabels = await LoadOwnerLabelsAsync(tenantId, [template.OwnerCredentialId], ct);
        return Result<MessageTemplateResponse>.Success(ToResponse(template, ownerLabels));
    }

    public async Task<Result<MessageTemplateResponse>> CreateTemplateAsync(
        CreateMessageTemplateRequest request,
        CancellationToken ct = default)
    {
        var accessResult = await ResolveTemplateAccessAsync(request.Metadata);
        if (!accessResult.IsSuccess)
            return Result<MessageTemplateResponse>.Failure(accessResult.Message ?? "Tenant could not be resolved", accessResult.StatusCode);

        var access = accessResult.Data!;
        var tenantId = access.TenantId;
        await EnsureTenantTemplatesAsync(tenantId, ct);

        var requestedType = NormalizeTemplateType(request.TemplateType);
        if (!access.IsAdmin)
        {
            if (requestedType != MessageTemplateTypes.User)
                return Result<MessageTemplateResponse>.Forbidden("Tenant and system templates require an admin context.");

            request.OwnerCredentialId = access.CredentialId;
            request.TemplateType = MessageTemplateTypes.User;
        }

        var validation = await ValidateCreateAsync(tenantId, request, ct);
        if (validation.Count > 0)
            return Result<MessageTemplateResponse>.ValidationError(validation);

        var now = DateTime.UtcNow;
        var template = new MessageTemplate
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TemplateType = NormalizeTemplateType(request.TemplateType),
            Key = NormalizeKey(request.Key),
            Name = request.Name.Trim(),
            Description = NormalizeNullable(request.Description),
            Subject = NormalizeNullable(request.Subject),
            Body = request.Body.Trim(),
            RequiredVariablesJson = SerializeVariables(request.RequiredVariables),
            OwnerCredentialId = NormalizeTemplateType(request.TemplateType) == MessageTemplateTypes.User
                ? request.OwnerCredentialId
                : null,
            IsDefault = request.IsDefault,
            IsLocked = false,
            IsEnabled = request.IsEnabled,
            CreatedAt = now,
            ModifiedAt = now,
            ConcurrencyStamp = Guid.NewGuid()
        };

        dataContext.Add(template);
        var saveResult = await dataContext.SaveChangesAsync(ct);
        if (!saveResult.IsSuccess)
            return Result<MessageTemplateResponse>.Failure(saveResult.Message ?? "Message template could not be created.", saveResult.StatusCode);

        var ownerLabels = await LoadOwnerLabelsAsync(tenantId, [template.OwnerCredentialId], ct);
        return Result<MessageTemplateResponse>.Success(ToResponse(template, ownerLabels), 201, "Message template created.");
    }

    public async Task<Result<MessageTemplateResponse>> UpdateTemplateAsync(
        UpdateMessageTemplateRequest request,
        CancellationToken ct = default)
    {
        var accessResult = await ResolveTemplateAccessAsync(request.Metadata);
        if (!accessResult.IsSuccess)
            return Result<MessageTemplateResponse>.Failure(accessResult.Message ?? "Tenant could not be resolved", accessResult.StatusCode);

        var access = accessResult.Data!;
        var tenantId = access.TenantId;
        await EnsureTenantTemplatesAsync(tenantId, ct);

        var template = await FindTemplateByIdAsync(tenantId, request.TemplateId, includeInactive: access.IsAdmin, ct);
        if (template is null)
            return Result<MessageTemplateResponse>.NotFound("Message template not found.");

        if (IsSystemTemplate(template))
            return Result<MessageTemplateResponse>.Forbidden("System templates cannot be edited. Clone the template first.");

        if (!CanMutateTemplate(template, access))
            return Result<MessageTemplateResponse>.Forbidden("Only the owner can edit this user template.");

        var validation = await ValidateUpdateAsync(tenantId, template, request, ct);
        if (validation.Count > 0)
            return Result<MessageTemplateResponse>.ValidationError(validation);

        if (request.Key is not null)
            template.Key = NormalizeKey(request.Key);

        if (request.Name is not null)
            template.Name = request.Name.Trim();

        if (request.Description is not null)
            template.Description = NormalizeNullable(request.Description);

        if (request.Subject is not null)
            template.Subject = NormalizeNullable(request.Subject);

        if (request.Body is not null)
            template.Body = request.Body.Trim();

        if (request.RequiredVariables is not null)
            template.RequiredVariablesJson = SerializeVariables(request.RequiredVariables);

        if (request.IsDefault is bool isDefault)
            template.IsDefault = isDefault;

        if (request.IsEnabled is bool isEnabled)
            template.IsEnabled = isEnabled;

        template.ModifiedAt = DateTime.UtcNow;
        template.ConcurrencyStamp = Guid.NewGuid();

        dataContext.Update(template);
        var saveResult = await dataContext.SaveChangesAsync(ct);
        if (!saveResult.IsSuccess)
            return Result<MessageTemplateResponse>.Failure(saveResult.Message ?? "Message template could not be updated.", saveResult.StatusCode);

        var ownerLabels = await LoadOwnerLabelsAsync(tenantId, [template.OwnerCredentialId], ct);
        return Result<MessageTemplateResponse>.Success(ToResponse(template, ownerLabels), "Message template updated.");
    }

    public async Task<Result<CmdResponse>> DeleteTemplateAsync(
        DeleteMessageTemplateRequest request,
        CancellationToken ct = default)
    {
        var accessResult = await ResolveTemplateAccessAsync(request.Metadata);
        if (!accessResult.IsSuccess)
            return Result<CmdResponse>.Failure(accessResult.Message ?? "Tenant could not be resolved", accessResult.StatusCode);

        var access = accessResult.Data!;
        var tenantId = access.TenantId;
        await EnsureTenantTemplatesAsync(tenantId, ct);

        var template = await FindTemplateByIdAsync(tenantId, request.TemplateId, includeInactive: access.IsAdmin, ct);
        if (template is null)
            return Result<CmdResponse>.NotFound("Message template not found.");

        if (IsSystemTemplate(template))
            return Result<CmdResponse>.Forbidden("System templates cannot be deleted.");

        if (!CanMutateTemplate(template, access))
            return Result<CmdResponse>.Forbidden("Only the owner can delete this user template.");

        template.IsEnabled = false;
        template.IsDeleted = true;
        template.DeletedAt = DateTime.UtcNow;
        template.ModifiedAt = template.DeletedAt;
        template.ConcurrencyStamp = Guid.NewGuid();

        dataContext.Update(template);
        var saveResult = await dataContext.SaveChangesAsync(ct);
        if (!saveResult.IsSuccess)
            return Result<CmdResponse>.Failure(saveResult.Message ?? "Message template could not be deleted.", saveResult.StatusCode);

        return Result<CmdResponse>.Success(new CmdResponse
        {
            HttpStatusCode = HttpStatusCode.OK,
            Message = "Message template deleted."
        });
    }

    public async Task<Result<MessageTemplateResponse>> CloneTemplateAsync(
        CloneMessageTemplateRequest request,
        CancellationToken ct = default)
    {
        var accessResult = await ResolveTemplateAccessAsync(request.Metadata);
        if (!accessResult.IsSuccess)
            return Result<MessageTemplateResponse>.Failure(accessResult.Message ?? "Tenant could not be resolved", accessResult.StatusCode);

        var access = accessResult.Data!;
        var tenantId = access.TenantId;
        await EnsureTenantTemplatesAsync(tenantId, ct);

        var source = await FindTemplateByIdAsync(tenantId, request.TemplateId, includeInactive: access.IsAdmin, ct);
        if (source is null)
            return Result<MessageTemplateResponse>.NotFound("Message template not found.");

        if (!access.IsAdmin && !CanUserAccessTemplate(source, access.CredentialId))
            return Result<MessageTemplateResponse>.Forbidden("Message template is not available to this credential.");

        var targetType = NormalizeTemplateType(request.TemplateType);
        if (!access.IsAdmin)
        {
            targetType = MessageTemplateTypes.User;
            request.OwnerCredentialId = access.CredentialId;
        }

        var key = NormalizeKey(request.Key ?? $"{source.Key}.copy");
        var createRequest = new CreateMessageTemplateRequest
        {
            Metadata = request.Metadata,
            TemplateType = targetType,
            OwnerCredentialId = targetType == MessageTemplateTypes.User ? request.OwnerCredentialId : null,
            Key = key,
            Name = string.IsNullOrWhiteSpace(request.Name) ? $"{source.Name} Copy" : request.Name.Trim(),
            Description = source.Description,
            Subject = source.Subject,
            Body = source.Body,
            RequiredVariables = DeserializeVariables(source.RequiredVariablesJson),
            IsDefault = false,
            IsEnabled = true
        };

        return await CreateTemplateAsync(createRequest, ct);
    }

    public async Task<Result<RenderMessageTemplateResponse>> RenderTemplateAsync(
        RenderMessageTemplateRequest request,
        CancellationToken ct = default)
    {
        var tenantContext = requestContextResolver.ResolveTenant(request.Metadata);
        if (!tenantContext.IsSuccess)
            return Result<RenderMessageTemplateResponse>.Failure(
                tenantContext.Message ?? "Tenant could not be resolved",
                tenantContext.StatusCode);

        var tenantResult = await ResolveTenantIdAsync(tenantContext.Data!.TenantId);
        if (!tenantResult.IsSuccess)
            return Result<RenderMessageTemplateResponse>.Failure(tenantResult.Message ?? "Tenant could not be resolved", tenantResult.StatusCode);

        var tenantId = tenantResult.Data;
        await EnsureTenantTemplatesAsync(tenantId, ct);

        var template = request.TemplateId is Guid templateId
            ? await FindTemplateByIdAsync(tenantId, templateId, includeInactive: false, ct)
            : await ResolveTemplateByKeyAsync(tenantId, tenantContext.Data.CredentialId, request.TemplateKey, ct);

        if (template is null)
            return Result<RenderMessageTemplateResponse>.NotFound("Message template not found.");

        if (template.TemplateType == MessageTemplateTypes.User &&
            template.OwnerCredentialId is Guid ownerCredentialId &&
            tenantContext.Data.CredentialId != ownerCredentialId)
        {
            return Result<RenderMessageTemplateResponse>.Forbidden("User template is not available to this credential.");
        }

        var renderResult = Render(template, request.TemplateVariables);
        if (renderResult.Errors.Count > 0)
            return Result<RenderMessageTemplateResponse>.ValidationError(renderResult.Errors);

        return Result<RenderMessageTemplateResponse>.Success(new RenderMessageTemplateResponse
        {
            TemplateId = template.Id,
            TemplateKey = template.Key,
            TemplateType = template.TemplateType,
            Subject = renderResult.Subject,
            Body = renderResult.Body,
            TemplateVariables = renderResult.Variables
        });
    }

    private async Task EnsureTenantTemplatesAsync(Guid tenantId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var templates = await dataContext.Query<MessageTemplate>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(template => template.TenantId == tenantId)
            .Where(template => !template.IsDeleted)
            .ToListAsync(ct);

        foreach (var systemTemplate in MessagingTemplateCatalog.SystemTemplates)
        {
            var existing = templates.FirstOrDefault(template =>
                template.SystemReferenceId == systemTemplate.SystemReferenceId ||
                (template.TemplateType == MessageTemplateTypes.System &&
                 string.Equals(template.Key, systemTemplate.Key, StringComparison.OrdinalIgnoreCase)));

            if (existing is not null)
                continue;

            var seeded = new MessageTemplate
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Key = systemTemplate.Key,
                Name = systemTemplate.Name,
                Description = systemTemplate.Description,
                TemplateType = MessageTemplateTypes.System,
                Subject = systemTemplate.Subject,
                Body = systemTemplate.Body,
                RequiredVariablesJson = SerializeVariables(systemTemplate.RequiredVariables),
                IsDefault = true,
                IsLocked = true,
                SystemReferenceId = systemTemplate.SystemReferenceId,
                IsEnabled = true,
                CreatedAt = now,
                ModifiedAt = now,
                ConcurrencyStamp = Guid.NewGuid()
            };
            dataContext.Add(seeded);
            templates.Add(seeded);
        }

        await BackfillLegacyRegistryTemplateAsync(
            tenantId,
            "MessagingService_Otp",
            MessageTemplateKeys.IdentityOtp,
            "Tenant OTP Override",
            "Backfilled tenant override from the legacy OTP registry setting.",
            ["Value"],
            templates,
            now,
            ct);
        await BackfillLegacyRegistryTemplateAsync(
            tenantId,
            "MessagingService_PasswordReset",
            MessageTemplateKeys.IdentityPasswordReset,
            "Tenant Password Reset Override",
            "Backfilled tenant override from the legacy password reset registry setting.",
            ["Token"],
            templates,
            now,
            ct);

        var saveResult = await dataContext.SaveChangesAsync(ct);
        if (!saveResult.IsSuccess)
        {
            logger.LogWarning("Messaging templates could not be seeded for tenant {TenantId}: {Message}", tenantId, saveResult.Message);
        }
    }

    private async Task BackfillLegacyRegistryTemplateAsync(
        Guid tenantId,
        string groupName,
        string templateKey,
        string name,
        string description,
        IReadOnlyList<string> variables,
        ICollection<MessageTemplate> existingTemplates,
        DateTime now,
        CancellationToken ct)
    {
        if (existingTemplates.Any(template =>
                template.TemplateType == MessageTemplateTypes.Tenant &&
                string.Equals(template.Key, templateKey, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        var legacyConfig = await dataContext.Query<RegistryConfiguration>()
            .IgnoreQueryFilters()
            .NoCache()
            .Include(config => config.Group)
            .Where(config => config.TenantId == tenantId)
            .Where(config => !config.IsDeleted)
            .Where(config => config.Key == "MessageTemplate")
            .Where(config => config.Group != null && config.Group.Name == groupName)
            .FirstOrDefaultAsync(ct);

        if (string.IsNullOrWhiteSpace(legacyConfig?.Value))
            return;

        var systemTemplate = MessagingTemplateCatalog.FindSystemTemplate(templateKey);
        dataContext.Add(new MessageTemplate
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TemplateType = MessageTemplateTypes.Tenant,
            Key = templateKey,
            Name = name,
            Description = description,
            Subject = systemTemplate?.Subject,
            Body = legacyConfig.Value.Trim(),
            RequiredVariablesJson = SerializeVariables(variables),
            IsDefault = true,
            IsLocked = false,
            IsEnabled = true,
            CreatedAt = now,
            ModifiedAt = now,
            ConcurrencyStamp = Guid.NewGuid()
        });
    }

    private async Task<List<MessageTemplate>> LoadTemplatesAsync(
        Guid tenantId,
        bool includeInactive,
        CancellationToken ct)
    {
        var query = dataContext.Query<MessageTemplate>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(template => template.TenantId == tenantId)
            .Where(template => !template.IsDeleted);

        if (!includeInactive)
        {
            query = query.Where(template => template.IsEnabled);
        }

        return await query.Take(1000).ToListAsync(ct);
    }

    private async Task<MessageTemplate?> FindTemplateByIdAsync(
        Guid tenantId,
        Guid templateId,
        bool includeInactive,
        CancellationToken ct)
    {
        var query = dataContext.Query<MessageTemplate>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(template => template.TenantId == tenantId)
            .Where(template => template.Id == templateId)
            .Where(template => !template.IsDeleted);

        if (!includeInactive)
        {
            query = query.Where(template => template.IsEnabled);
        }

        return await query.FirstOrDefaultAsync(ct);
    }

    private async Task<MessageTemplate?> ResolveTemplateByKeyAsync(
        Guid tenantId,
        Guid? credentialId,
        string? templateKey,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(templateKey))
            return null;

        var key = NormalizeKey(templateKey);
        var templates = await dataContext.Query<MessageTemplate>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(template => template.TenantId == tenantId)
            .Where(template => template.Key == key)
            .Where(template => !template.IsDeleted && template.IsEnabled)
            .ToListAsync(ct);

        if (credentialId is Guid credential)
        {
            var userTemplate = templates.FirstOrDefault(template =>
                template.TemplateType == MessageTemplateTypes.User &&
                template.OwnerCredentialId == credential);
            if (userTemplate is not null)
                return userTemplate;
        }

        return templates.FirstOrDefault(template => template.TemplateType == MessageTemplateTypes.Tenant)
               ?? templates.FirstOrDefault(template => template.TemplateType == MessageTemplateTypes.System);
    }

    private async Task<Dictionary<string, string[]>> ValidateCreateAsync(
        Guid tenantId,
        CreateMessageTemplateRequest request,
        CancellationToken ct)
    {
        var errors = ValidateShape(
            request.TemplateType,
            request.Key,
            request.Name,
            request.Description,
            request.Subject,
            request.Body,
            request.RequiredVariables);

        var type = NormalizeTemplateType(request.TemplateType);
        ValidateTemplateTypeScope(errors, type, request.OwnerCredentialId);

        if (type == MessageTemplateTypes.System)
            errors["TemplateType"] = ["System templates are seeded by Messaging and cannot be created through the API."];

        if (!errors.ContainsKey("Key") &&
            !errors.ContainsKey("TemplateType") &&
            await HasDuplicateKeyAsync(tenantId, type, request.OwnerCredentialId, NormalizeKey(request.Key), exceptTemplateId: null, ct))
        {
            errors["Key"] = ["An active template with this key already exists in the selected scope."];
        }

        if (type == MessageTemplateTypes.User && request.OwnerCredentialId is Guid ownerCredentialId)
        {
            await ValidateOwnerAsync(tenantId, ownerCredentialId, errors, ct);
        }

        return errors;
    }

    private async Task<Dictionary<string, string[]>> ValidateUpdateAsync(
        Guid tenantId,
        MessageTemplate existing,
        UpdateMessageTemplateRequest request,
        CancellationToken ct)
    {
        var key = request.Key ?? existing.Key;
        var name = request.Name ?? existing.Name;
        var description = request.Description ?? existing.Description;
        var subject = request.Subject ?? existing.Subject;
        var body = request.Body ?? existing.Body;
        var variables = request.RequiredVariables ?? DeserializeVariables(existing.RequiredVariablesJson);

        var errors = ValidateShape(existing.TemplateType, key, name, description, subject, body, variables);
        if (!errors.ContainsKey("Key") &&
            await HasDuplicateKeyAsync(tenantId, existing.TemplateType, existing.OwnerCredentialId, NormalizeKey(key), existing.Id, ct))
        {
            errors["Key"] = ["An active template with this key already exists in the selected scope."];
        }

        return errors;
    }

    private static Dictionary<string, string[]> ValidateShape(
        string templateType,
        string key,
        string name,
        string? description,
        string? subject,
        string body,
        IReadOnlyCollection<string> requiredVariables)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (!IsKnownTemplateType(templateType))
            errors["TemplateType"] = ["Template type must be System, Tenant, or User."];

        if (string.IsNullOrWhiteSpace(key))
        {
            errors["Key"] = ["Template key is required."];
        }
        else
        {
            var normalizedKey = NormalizeKey(key);
            if (normalizedKey.Length > MaxKeyLength)
                errors["Key"] = [$"Template key cannot exceed {MaxKeyLength} characters."];
            else if (normalizedKey.Any(character => !char.IsLetterOrDigit(character) && character is not '.' and not '-' and not '_'))
                errors["Key"] = ["Template key may only contain letters, numbers, dots, dashes, and underscores."];
        }

        if (string.IsNullOrWhiteSpace(name))
            errors["Name"] = ["Template name is required."];
        else if (name.Trim().Length > MaxNameLength)
            errors["Name"] = [$"Template name cannot exceed {MaxNameLength} characters."];

        if (description?.Length > MaxDescriptionLength)
            errors["Description"] = [$"Template description cannot exceed {MaxDescriptionLength} characters."];

        if (subject?.Length > MaxSubjectLength)
            errors["Subject"] = [$"Template subject cannot exceed {MaxSubjectLength} characters."];

        if (string.IsNullOrWhiteSpace(body))
            errors["Body"] = ["Template body is required."];
        else if (body.Trim().Length > MaxBodyLength)
            errors["Body"] = [$"Template body cannot exceed {MaxBodyLength} characters."];

        var invalidVariables = requiredVariables
            .Where(variable => string.IsNullOrWhiteSpace(variable) ||
                               variable.Any(character => !char.IsLetterOrDigit(character) && character is not '_' and not '-' and not '.'))
            .ToList();
        if (invalidVariables.Count > 0)
            errors["RequiredVariables"] = ["Required variables may only contain letters, numbers, dots, dashes, and underscores."];

        return errors;
    }

    private static void ValidateTemplateTypeScope(
        Dictionary<string, string[]> errors,
        string templateType,
        Guid? ownerCredentialId)
    {
        if (templateType == MessageTemplateTypes.User && ownerCredentialId is null)
        {
            errors["OwnerCredentialId"] = ["User templates require an owner credential."];
        }

        if (templateType == MessageTemplateTypes.Tenant && ownerCredentialId is not null)
        {
            errors["OwnerCredentialId"] = ["Tenant templates cannot have a user owner."];
        }
    }

    private async Task ValidateOwnerAsync(
        Guid tenantId,
        Guid ownerCredentialId,
        Dictionary<string, string[]> errors,
        CancellationToken ct)
    {
        var exists = await dataContext.Query<IdentityCredential>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(credential => credential.TenantId == tenantId)
            .Where(credential => credential.Id == ownerCredentialId)
            .Where(credential => !credential.IsDeleted)
            .AnyAsync(ct);

        if (!exists)
            errors["OwnerCredentialId"] = ["Owner credential was not found in this tenant."];
    }

    private async Task<bool> HasDuplicateKeyAsync(
        Guid tenantId,
        string templateType,
        Guid? ownerCredentialId,
        string key,
        Guid? exceptTemplateId,
        CancellationToken ct)
    {
        var query = dataContext.Query<MessageTemplate>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(template => template.TenantId == tenantId)
            .Where(template => !template.IsDeleted)
            .Where(template => template.IsEnabled)
            .Where(template => template.Key == key);

        if (templateType == MessageTemplateTypes.User)
        {
            query = query.Where(template =>
                template.TemplateType == MessageTemplateTypes.User &&
                template.OwnerCredentialId == ownerCredentialId);
        }
        else
        {
            query = query.Where(template =>
                template.TemplateType == templateType &&
                template.OwnerCredentialId == null);
        }

        if (exceptTemplateId is Guid existingId)
        {
            query = query.Where(template => template.Id != existingId);
        }

        return await query.AnyAsync(ct);
    }

    private TemplateRenderResult Render(
        MessageTemplate template,
        IReadOnlyDictionary<string, string> variables)
    {
        var values = new Dictionary<string, string>(variables, StringComparer.OrdinalIgnoreCase);
        var requiredVariables = DeserializeVariables(template.RequiredVariablesJson)
            .Concat(ExtractTokens(template.Subject))
            .Concat(ExtractTokens(template.Body))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var missing = requiredVariables
            .Where(variable => !values.ContainsKey(variable) || string.IsNullOrWhiteSpace(values[variable]))
            .ToList();
        if (missing.Count > 0)
        {
            return TemplateRenderResult.Invalid(new Dictionary<string, string[]>
            {
                ["TemplateVariables"] = [$"Missing required template variable(s): {string.Join(", ", missing)}."]
            });
        }

        var subject = ReplaceTokens(template.Subject, values);
        var body = ReplaceTokens(template.Body, values) ?? string.Empty;
        if (subject?.Length > MaxSubjectLength)
        {
            return TemplateRenderResult.Invalid(new Dictionary<string, string[]>
            {
                ["Subject"] = [$"Rendered subject cannot exceed {MaxSubjectLength} characters."]
            });
        }

        if (body.Length > MaxBodyLength)
        {
            return TemplateRenderResult.Invalid(new Dictionary<string, string[]>
            {
                ["Body"] = [$"Rendered message cannot exceed {MaxBodyLength} characters."]
            });
        }

        return TemplateRenderResult.Valid(subject, body, values);
    }

    private static string? ReplaceTokens(string? value, IReadOnlyDictionary<string, string> variables)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        return TokenRegex.Replace(value, match =>
        {
            var tokenName = match.Groups[1].Value;
            return variables.TryGetValue(tokenName, out var replacement) ? replacement : match.Value;
        });
    }

    private static List<string> ExtractTokens(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return [];

        return TokenRegex.Matches(value)
            .Select(match => match.Groups[1].Value)
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<Dictionary<Guid, string>> LoadOwnerLabelsAsync(
        Guid tenantId,
        IEnumerable<Guid?> ownerCredentialIds,
        CancellationToken ct)
    {
        var ids = ownerCredentialIds
            .OfType<Guid>()
            .Distinct()
            .ToList();
        if (ids.Count == 0)
            return [];

        var credentials = await dataContext.Query<IdentityCredential>()
            .IgnoreQueryFilters()
            .NoCache()
            .Include(credential => credential.IdentityInfo)
            .Where(credential => credential.TenantId == tenantId)
            .Where(credential => ids.Contains(credential.Id))
            .ToListAsync(ct);

        return credentials.ToDictionary(credential => credential.Id, BuildOwnerLabel);
    }

    private static MessageTemplateResponse ToResponse(
        MessageTemplate template,
        IReadOnlyDictionary<Guid, string> ownerLabels) =>
        new()
        {
            Id = template.Id,
            TenantId = template.TenantId,
            Key = template.Key,
            Name = template.Name,
            Description = template.Description,
            TemplateType = template.TemplateType,
            Subject = template.Subject,
            Body = template.Body,
            RequiredVariables = DeserializeVariables(template.RequiredVariablesJson),
            OwnerCredentialId = template.OwnerCredentialId,
            OwnerLabel = template.OwnerCredentialId is Guid ownerCredentialId
                ? ownerLabels.GetValueOrDefault(ownerCredentialId) ?? ownerCredentialId.ToString()[..8]
                : null,
            IsDefault = template.IsDefault,
            IsLocked = template.IsLocked,
            IsEnabled = template.IsEnabled,
            IsDeleted = template.IsDeleted,
            SystemReferenceId = template.SystemReferenceId,
            CreatedAt = template.CreatedAt,
            ModifiedAt = template.ModifiedAt
        };

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

    private async Task<Result<TemplateAccessContext>> ResolveTemplateAccessAsync(RequestMetadata? metadata)
    {
        var adminContext = requestContextResolver.ResolveAdmin(metadata);
        if (adminContext.IsSuccess)
        {
            var tenant = await ResolveTenantIdAsync(adminContext.Data!.TenantId);
            return tenant.IsSuccess
                ? Result<TemplateAccessContext>.Success(new(tenant.Data, adminContext.Data.CredentialId, IsAdmin: true))
                : Result<TemplateAccessContext>.Failure(tenant.Message ?? "Tenant could not be resolved", tenant.StatusCode);
        }

        var userContext = requestContextResolver.Resolve(metadata);
        if (!userContext.IsSuccess)
        {
            return Result<TemplateAccessContext>.Failure(
                userContext.Message ?? adminContext.Message ?? "Messaging templates require an authenticated context",
                userContext.StatusCode);
        }

        var userTenant = await ResolveTenantIdAsync(userContext.Data!.TenantId);
        return userTenant.IsSuccess
            ? Result<TemplateAccessContext>.Success(new(userTenant.Data, userContext.Data.CredentialId, IsAdmin: false))
            : Result<TemplateAccessContext>.Failure(userTenant.Message ?? "Tenant could not be resolved", userTenant.StatusCode);
    }

    private async Task<Result<Guid>> ResolveAdminTenantIdAsync(RequestMetadata? metadata)
    {
        var adminContext = requestContextResolver.ResolveAdmin(metadata);
        if (!adminContext.IsSuccess)
            return Result<Guid>.Failure(
                adminContext.Message ?? "Messaging templates require an admin context",
                adminContext.StatusCode);

        return await ResolveTenantIdAsync(adminContext.Data!.TenantId);
    }

    private static string NormalizeTemplateType(string? templateType)
    {
        if (string.Equals(templateType, MessageTemplateTypes.System, StringComparison.OrdinalIgnoreCase))
            return MessageTemplateTypes.System;

        if (string.Equals(templateType, MessageTemplateTypes.User, StringComparison.OrdinalIgnoreCase))
            return MessageTemplateTypes.User;

        return MessageTemplateTypes.Tenant;
    }

    private static bool IsKnownTemplateType(string? templateType) =>
        string.IsNullOrWhiteSpace(templateType) ||
        MessageTemplateTypes.All.Contains(templateType.Trim(), StringComparer.OrdinalIgnoreCase);

    private static string NormalizeKey(string key) => key.Trim().ToLowerInvariant();

    private static string? NormalizeNullable(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsSystemTemplate(MessageTemplate template) =>
        template.IsLocked || template.TemplateType == MessageTemplateTypes.System;

    private static bool CanUserAccessTemplate(MessageTemplate template, Guid? credentialId) =>
        template.TemplateType != MessageTemplateTypes.User ||
        (credentialId is Guid id && template.OwnerCredentialId == id);

    private static bool CanMutateTemplate(MessageTemplate template, TemplateAccessContext access) =>
        access.IsAdmin ||
        (template.TemplateType == MessageTemplateTypes.User &&
         access.CredentialId is Guid credentialId &&
         template.OwnerCredentialId == credentialId);

    private static int TemplateTypeRank(string templateType) =>
        templateType switch
        {
            MessageTemplateTypes.System => 0,
            MessageTemplateTypes.Tenant => 1,
            MessageTemplateTypes.User => 2,
            _ => 3
        };

    private static string SerializeVariables(IEnumerable<string> variables) =>
        JsonSerializer.Serialize(
            variables
                .Where(variable => !string.IsNullOrWhiteSpace(variable))
                .Select(variable => variable.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            JsonOptions);

    private static List<string> DeserializeVariables(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static string BuildOwnerLabel(IdentityCredential credential)
    {
        if (!string.IsNullOrWhiteSpace(credential.IdentityInfo?.FullName))
            return credential.IdentityInfo.FullName;

        if (!string.IsNullOrWhiteSpace(credential.IdentityInfo?.IdentityName))
            return credential.IdentityInfo.IdentityName;

        if (!string.IsNullOrWhiteSpace(credential.UserAlias))
            return credential.UserAlias;

        if (!string.IsNullOrWhiteSpace(credential.UserName))
            return credential.UserName;

        return credential.Id.ToString()[..8];
    }

    private sealed record TemplateRenderResult(
        bool IsSuccess,
        string? Subject,
        string Body,
        Dictionary<string, string> Variables,
        Dictionary<string, string[]> Errors)
    {
        public static TemplateRenderResult Valid(
            string? subject,
            string body,
            Dictionary<string, string> variables) =>
            new(true, subject, body, variables, []);

        public static TemplateRenderResult Invalid(Dictionary<string, string[]> errors) =>
            new(false, null, string.Empty, [], errors);
    }
}
