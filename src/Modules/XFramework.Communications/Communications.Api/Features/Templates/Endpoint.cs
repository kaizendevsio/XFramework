using Communications.Domain.Shared.Contracts.Requests.Templates;
using Communications.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Communications.Api.Features.Templates;

public static class GetMessageTemplatesEndpoint
{
    [BoltHandler(
        TenantAccessMode = TenantAccessMode.DelegatedTenant,
        RequiredServiceScopes = [XFrameworkServiceScopes.CommunicationsChat],
        RequiredCrossTenantActorCapabilities = [XFrameworkActorCapabilities.IdentityTenantsManage])]
    [MapGet("/api/communications/templates", Tags = ["Communications Templates"],
        Summary = "List Communications templates",
        Description = "Returns system, tenant, and user Communications templates for the authenticated tenant.")]
    public static Task<Result<GetMessageTemplatesResponse>> Handle(
        GetMessageTemplatesRequest request,
        ICommunicationsTemplateService templateService,
        CancellationToken ct) =>
        templateService.GetTemplatesAsync(request, ct);
}

public static class GetMessageTemplateEndpoint
{
    [BoltHandler(
        TenantAccessMode = TenantAccessMode.DelegatedTenant,
        RequiredServiceScopes = [XFrameworkServiceScopes.CommunicationsChat],
        RequiredCrossTenantActorCapabilities = [XFrameworkActorCapabilities.IdentityTenantsManage])]
    [MapGet("/api/communications/templates/{templateId:guid}", Tags = ["Communications Templates"],
        Summary = "Get Communications template",
        Description = "Returns a single Communications template for the authenticated tenant.")]
    public static Task<Result<MessageTemplateResponse>> Handle(
        GetMessageTemplateRequest request,
        ICommunicationsTemplateService templateService,
        CancellationToken ct) =>
        templateService.GetTemplateAsync(request, ct);
}

public static class CreateMessageTemplateEndpoint
{
    [BoltHandler(
        TenantAccessMode = TenantAccessMode.DelegatedTenant,
        RequiredServiceScopes = [XFrameworkServiceScopes.CommunicationsChat],
        RequiredCrossTenantActorCapabilities = [XFrameworkActorCapabilities.IdentityTenantsManage])]
    [MapPost("/api/communications/templates", Tags = ["Communications Templates"],
        Summary = "Create Communications template",
        Description = "Creates a tenant or user Communications template.")]
    public static Task<Result<MessageTemplateResponse>> Handle(
        CreateMessageTemplateRequest request,
        ICommunicationsTemplateService templateService,
        CancellationToken ct) =>
        templateService.CreateTemplateAsync(request, ct);
}

public static class UpdateMessageTemplateEndpoint
{
    [BoltHandler(
        TenantAccessMode = TenantAccessMode.DelegatedTenant,
        RequiredServiceScopes = [XFrameworkServiceScopes.CommunicationsChat],
        RequiredCrossTenantActorCapabilities = [XFrameworkActorCapabilities.IdentityTenantsManage])]
    [MapPut("/api/communications/templates/{templateId:guid}", Tags = ["Communications Templates"],
        Summary = "Update Communications template",
        Description = "Updates a configurable tenant or user Communications template.")]
    public static Task<Result<MessageTemplateResponse>> Handle(
        UpdateMessageTemplateRequest request,
        ICommunicationsTemplateService templateService,
        CancellationToken ct) =>
        templateService.UpdateTemplateAsync(request, ct);
}

public static class DeleteMessageTemplateEndpoint
{
    [BoltHandler(
        TenantAccessMode = TenantAccessMode.DelegatedTenant,
        RequiredServiceScopes = [XFrameworkServiceScopes.CommunicationsChat],
        RequiredCrossTenantActorCapabilities = [XFrameworkActorCapabilities.IdentityTenantsManage])]
    [MapDelete("/api/communications/templates/{templateId:guid}", Tags = ["Communications Templates"],
        Summary = "Delete Communications template",
        Description = "Soft-deletes a configurable Communications template.")]
    public static Task<Result<CmdResponse>> Handle(
        DeleteMessageTemplateRequest request,
        ICommunicationsTemplateService templateService,
        CancellationToken ct) =>
        templateService.DeleteTemplateAsync(request, ct);
}

public static class CloneMessageTemplateEndpoint
{
    [BoltHandler(
        TenantAccessMode = TenantAccessMode.DelegatedTenant,
        RequiredServiceScopes = [XFrameworkServiceScopes.CommunicationsChat],
        RequiredCrossTenantActorCapabilities = [XFrameworkActorCapabilities.IdentityTenantsManage])]
    [MapPost("/api/communications/templates/{templateId:guid}/clone", Tags = ["Communications Templates"],
        Summary = "Clone Communications template",
        Description = "Clones a system, tenant, or user template into a configurable tenant or user template.")]
    public static Task<Result<MessageTemplateResponse>> Handle(
        CloneMessageTemplateRequest request,
        ICommunicationsTemplateService templateService,
        CancellationToken ct) =>
        templateService.CloneTemplateAsync(request, ct);
}

public static class RenderMessageTemplateEndpoint
{
    [BoltHandler(
        TenantAccessMode = TenantAccessMode.DelegatedTenant,
        RequiredServiceScopes = [XFrameworkServiceScopes.CommunicationsChat],
        RequiredCrossTenantActorCapabilities = [XFrameworkActorCapabilities.IdentityTenantsManage])]
    [MapPost("/api/communications/templates/render", Tags = ["Communications Templates"],
        Summary = "Render Communications template",
        Description = "Renders a Communications template with supplied variables using server-side validation.")]
    public static Task<Result<RenderMessageTemplateResponse>> Handle(
        RenderMessageTemplateRequest request,
        ICommunicationsTemplateService templateService,
        CancellationToken ct) =>
        templateService.RenderTemplateAsync(request, ct);
}
