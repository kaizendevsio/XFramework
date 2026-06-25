using Messaging.Domain.Shared.Contracts.Requests.Templates;
using Messaging.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Messaging.Api.Features.Templates;

public static class GetMessageTemplatesEndpoint
{
    [BoltHandler]
    [MapGet("/api/messaging/templates", Tags = ["Messaging Templates"],
        Summary = "List Messaging templates",
        Description = "Returns system, tenant, and user Messaging templates for the authenticated tenant.")]
    public static Task<Result<GetMessageTemplatesResponse>> Handle(
        GetMessageTemplatesRequest request,
        IMessagingTemplateService templateService,
        CancellationToken ct) =>
        templateService.GetTemplatesAsync(request, ct);
}

public static class GetMessageTemplateEndpoint
{
    [BoltHandler]
    [MapGet("/api/messaging/templates/{templateId:guid}", Tags = ["Messaging Templates"],
        Summary = "Get Messaging template",
        Description = "Returns a single Messaging template for the authenticated tenant.")]
    public static Task<Result<MessageTemplateResponse>> Handle(
        GetMessageTemplateRequest request,
        IMessagingTemplateService templateService,
        CancellationToken ct) =>
        templateService.GetTemplateAsync(request, ct);
}

public static class CreateMessageTemplateEndpoint
{
    [BoltHandler]
    [MapPost("/api/messaging/templates", Tags = ["Messaging Templates"],
        Summary = "Create Messaging template",
        Description = "Creates a tenant or user Messaging template.")]
    public static Task<Result<MessageTemplateResponse>> Handle(
        CreateMessageTemplateRequest request,
        IMessagingTemplateService templateService,
        CancellationToken ct) =>
        templateService.CreateTemplateAsync(request, ct);
}

public static class UpdateMessageTemplateEndpoint
{
    [BoltHandler]
    [MapPut("/api/messaging/templates/{templateId:guid}", Tags = ["Messaging Templates"],
        Summary = "Update Messaging template",
        Description = "Updates a configurable tenant or user Messaging template.")]
    public static Task<Result<MessageTemplateResponse>> Handle(
        UpdateMessageTemplateRequest request,
        IMessagingTemplateService templateService,
        CancellationToken ct) =>
        templateService.UpdateTemplateAsync(request, ct);
}

public static class DeleteMessageTemplateEndpoint
{
    [BoltHandler]
    [MapDelete("/api/messaging/templates/{templateId:guid}", Tags = ["Messaging Templates"],
        Summary = "Delete Messaging template",
        Description = "Soft-deletes a configurable Messaging template.")]
    public static Task<Result<CmdResponse>> Handle(
        DeleteMessageTemplateRequest request,
        IMessagingTemplateService templateService,
        CancellationToken ct) =>
        templateService.DeleteTemplateAsync(request, ct);
}

public static class CloneMessageTemplateEndpoint
{
    [BoltHandler]
    [MapPost("/api/messaging/templates/{templateId:guid}/clone", Tags = ["Messaging Templates"],
        Summary = "Clone Messaging template",
        Description = "Clones a system, tenant, or user template into a configurable tenant or user template.")]
    public static Task<Result<MessageTemplateResponse>> Handle(
        CloneMessageTemplateRequest request,
        IMessagingTemplateService templateService,
        CancellationToken ct) =>
        templateService.CloneTemplateAsync(request, ct);
}

public static class RenderMessageTemplateEndpoint
{
    [BoltHandler]
    [MapPost("/api/messaging/templates/render", Tags = ["Messaging Templates"],
        Summary = "Render Messaging template",
        Description = "Renders a Messaging template with supplied variables using server-side validation.")]
    public static Task<Result<RenderMessageTemplateResponse>> Handle(
        RenderMessageTemplateRequest request,
        IMessagingTemplateService templateService,
        CancellationToken ct) =>
        templateService.RenderTemplateAsync(request, ct);
}
