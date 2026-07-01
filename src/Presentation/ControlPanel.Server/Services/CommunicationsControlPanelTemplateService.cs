using Communications.Domain.Shared.Contracts.Requests.Templates;
using Communications.Domain.Shared.Contracts.Responses;
using Communications.Integration.Drivers;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Responses;

namespace ControlPanel.Server.Services;

public sealed class CommunicationsControlPanelTemplateService(
    ICommunicationsServiceWrapper communications,
    RequestMetadata metadata,
    TenantFilterService tenantFilter)
{
    public async Task<CommunicationsTemplatesLoadResult> GetTemplatesAsync(CancellationToken ct = default)
    {
        if (tenantFilter.SelectedTenantId is not Guid tenantId)
        {
            return CommunicationsTemplatesLoadResult.Failure("Select a tenant before loading Communications templates.");
        }

        var response = await communications.GetMessageTemplatesAsync(
            new GetMessageTemplatesRequest
            {
                Metadata = BuildMetadata(tenantId),
                IncludeInactive = true,
                PageSize = 500
            },
            ct);

        return response is { IsSuccess: true, Response: not null }
            ? CommunicationsTemplatesLoadResult.Success(response.Response)
            : CommunicationsTemplatesLoadResult.Failure(NormalizeFailureMessage(
                response.Message,
                "Communications templates could not be loaded."));
    }

    public async Task<CommunicationsTemplateMutationResult> CreateTemplateAsync(
        CreateMessageTemplateRequest request,
        CancellationToken ct = default)
    {
        if (tenantFilter.SelectedTenantId is not Guid tenantId)
        {
            return CommunicationsTemplateMutationResult.Failure("Select a tenant before creating a Communications template.");
        }

        request.Metadata = BuildMetadata(tenantId);
        var response = await communications.CreateMessageTemplateAsync(request, ct);
        return ToMutationResult(response, "Communications template created.");
    }

    public async Task<CommunicationsTemplateMutationResult> UpdateTemplateAsync(
        UpdateMessageTemplateRequest request,
        CancellationToken ct = default)
    {
        if (tenantFilter.SelectedTenantId is not Guid tenantId)
        {
            return CommunicationsTemplateMutationResult.Failure("Select a tenant before updating a Communications template.");
        }

        request.Metadata = BuildMetadata(tenantId);
        var response = await communications.UpdateMessageTemplateAsync(request, ct);
        return ToMutationResult(response, "Communications template updated.");
    }

    public async Task<CommunicationsTemplateMutationResult> CloneTemplateAsync(
        CloneMessageTemplateRequest request,
        CancellationToken ct = default)
    {
        if (tenantFilter.SelectedTenantId is not Guid tenantId)
        {
            return CommunicationsTemplateMutationResult.Failure("Select a tenant before cloning a Communications template.");
        }

        request.Metadata = BuildMetadata(tenantId);
        var response = await communications.CloneMessageTemplateAsync(request, ct);
        return ToMutationResult(response, "Communications template cloned.");
    }

    public async Task<CommunicationsTemplateMutationResult> DeleteTemplateAsync(
        Guid templateId,
        CancellationToken ct = default)
    {
        if (tenantFilter.SelectedTenantId is not Guid tenantId)
        {
            return CommunicationsTemplateMutationResult.Failure("Select a tenant before deleting a Communications template.");
        }

        var response = await communications.DeleteMessageTemplateAsync(
            new DeleteMessageTemplateRequest
            {
                Metadata = BuildMetadata(tenantId),
                TemplateId = templateId
            },
            ct);

        return response.IsSuccess
            ? CommunicationsTemplateMutationResult.Success(null, response.Message ?? "Communications template deleted.")
            : CommunicationsTemplateMutationResult.Failure(NormalizeFailureMessage(
                response.Message,
                "Communications template could not be deleted."));
    }

    private CommunicationsTemplateMutationResult ToMutationResult(
        CmdResponse<MessageTemplateResponse> response,
        string successMessage) =>
        response is { IsSuccess: true, Response: not null }
            ? CommunicationsTemplateMutationResult.Success(response.Response, response.Message ?? successMessage)
            : CommunicationsTemplateMutationResult.Failure(NormalizeFailureMessage(
                response.Message,
                "Communications template could not be saved."));

    private RequestMetadata BuildMetadata(Guid tenantId) => new()
    {
        TenantId = tenantId,
        CredentialId = metadata.CredentialId,
        SessionId = metadata.SessionId,
        RequestId = Guid.NewGuid(),
        Name = "ControlPanel",
        DeviceName = metadata.DeviceName,
        DeviceAgent = metadata.DeviceAgent,
        IpAddress = metadata.IpAddress
    };

    private static string NormalizeFailureMessage(string? message, string fallback)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return fallback;
        }

        return string.Equals(message, "NotFound", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(message, "NotImplemented", StringComparison.OrdinalIgnoreCase)
            ? "Communications templates service is unavailable. Check Communications service health and Bolt handler registration."
            : message;
    }
}

public sealed record CommunicationsTemplatesLoadResult(
    bool IsSuccess,
    GetMessageTemplatesResponse? Templates,
    string Message)
{
    public static CommunicationsTemplatesLoadResult Success(
        GetMessageTemplatesResponse templates,
        string message = "Communications templates loaded.") =>
        new(true, templates, message);

    public static CommunicationsTemplatesLoadResult Failure(string message) =>
        new(false, null, message);
}

public sealed record CommunicationsTemplateMutationResult(
    bool IsSuccess,
    MessageTemplateResponse? Template,
    string Message)
{
    public static CommunicationsTemplateMutationResult Success(
        MessageTemplateResponse? template,
        string message) =>
        new(true, template, message);

    public static CommunicationsTemplateMutationResult Failure(string message) =>
        new(false, null, message);
}
