using Messaging.Domain.Shared.Contracts.Requests.Templates;
using Messaging.Domain.Shared.Contracts.Responses;
using Messaging.Integration.Drivers;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Responses;

namespace ControlPanel.Server.Services;

public sealed class MessagingControlPanelTemplateService(
    IMessagingServiceWrapper messaging,
    RequestMetadata metadata,
    TenantFilterService tenantFilter)
{
    public async Task<MessagingTemplatesLoadResult> GetTemplatesAsync(CancellationToken ct = default)
    {
        if (tenantFilter.SelectedTenantId is not Guid tenantId)
        {
            return MessagingTemplatesLoadResult.Failure("Select a tenant before loading Messaging templates.");
        }

        var response = await messaging.GetMessageTemplatesAsync(
            new GetMessageTemplatesRequest
            {
                Metadata = BuildMetadata(tenantId),
                IncludeInactive = true,
                PageSize = 500
            },
            ct);

        return response is { IsSuccess: true, Response: not null }
            ? MessagingTemplatesLoadResult.Success(response.Response)
            : MessagingTemplatesLoadResult.Failure(NormalizeFailureMessage(
                response.Message,
                "Messaging templates could not be loaded."));
    }

    public async Task<MessagingTemplateMutationResult> CreateTemplateAsync(
        CreateMessageTemplateRequest request,
        CancellationToken ct = default)
    {
        if (tenantFilter.SelectedTenantId is not Guid tenantId)
        {
            return MessagingTemplateMutationResult.Failure("Select a tenant before creating a Messaging template.");
        }

        request.Metadata = BuildMetadata(tenantId);
        var response = await messaging.CreateMessageTemplateAsync(request, ct);
        return ToMutationResult(response, "Messaging template created.");
    }

    public async Task<MessagingTemplateMutationResult> UpdateTemplateAsync(
        UpdateMessageTemplateRequest request,
        CancellationToken ct = default)
    {
        if (tenantFilter.SelectedTenantId is not Guid tenantId)
        {
            return MessagingTemplateMutationResult.Failure("Select a tenant before updating a Messaging template.");
        }

        request.Metadata = BuildMetadata(tenantId);
        var response = await messaging.UpdateMessageTemplateAsync(request, ct);
        return ToMutationResult(response, "Messaging template updated.");
    }

    public async Task<MessagingTemplateMutationResult> CloneTemplateAsync(
        CloneMessageTemplateRequest request,
        CancellationToken ct = default)
    {
        if (tenantFilter.SelectedTenantId is not Guid tenantId)
        {
            return MessagingTemplateMutationResult.Failure("Select a tenant before cloning a Messaging template.");
        }

        request.Metadata = BuildMetadata(tenantId);
        var response = await messaging.CloneMessageTemplateAsync(request, ct);
        return ToMutationResult(response, "Messaging template cloned.");
    }

    public async Task<MessagingTemplateMutationResult> DeleteTemplateAsync(
        Guid templateId,
        CancellationToken ct = default)
    {
        if (tenantFilter.SelectedTenantId is not Guid tenantId)
        {
            return MessagingTemplateMutationResult.Failure("Select a tenant before deleting a Messaging template.");
        }

        var response = await messaging.DeleteMessageTemplateAsync(
            new DeleteMessageTemplateRequest
            {
                Metadata = BuildMetadata(tenantId),
                TemplateId = templateId
            },
            ct);

        return response.IsSuccess
            ? MessagingTemplateMutationResult.Success(null, response.Message ?? "Messaging template deleted.")
            : MessagingTemplateMutationResult.Failure(NormalizeFailureMessage(
                response.Message,
                "Messaging template could not be deleted."));
    }

    private MessagingTemplateMutationResult ToMutationResult(
        CmdResponse<MessageTemplateResponse> response,
        string successMessage) =>
        response is { IsSuccess: true, Response: not null }
            ? MessagingTemplateMutationResult.Success(response.Response, response.Message ?? successMessage)
            : MessagingTemplateMutationResult.Failure(NormalizeFailureMessage(
                response.Message,
                "Messaging template could not be saved."));

    private RequestMetadata BuildMetadata(Guid tenantId) => new()
    {
        TenantId = tenantId,
        CredentialId = metadata.CredentialId,
        SessionId = metadata.SessionId,
        RequestId = Guid.NewGuid(),
        Name = metadata.Name,
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
            ? "Messaging templates service is unavailable. Check Messaging service health and Bolt handler registration."
            : message;
    }
}

public sealed record MessagingTemplatesLoadResult(
    bool IsSuccess,
    GetMessageTemplatesResponse? Templates,
    string Message)
{
    public static MessagingTemplatesLoadResult Success(
        GetMessageTemplatesResponse templates,
        string message = "Messaging templates loaded.") =>
        new(true, templates, message);

    public static MessagingTemplatesLoadResult Failure(string message) =>
        new(false, null, message);
}

public sealed record MessagingTemplateMutationResult(
    bool IsSuccess,
    MessageTemplateResponse? Template,
    string Message)
{
    public static MessagingTemplateMutationResult Success(
        MessageTemplateResponse? template,
        string message) =>
        new(true, template, message);

    public static MessagingTemplateMutationResult Failure(string message) =>
        new(false, null, message);
}
