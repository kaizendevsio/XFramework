using SmsGateway.Api.Services;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Attributes;
using XFramework.Integration.Security;

namespace SmsGateway.Api.Features.Sms.ConfirmSent;

public static class ConfirmMessageSentEndpoint
{
    [MapPatch("/api/sms/messages/{id:guid}/sent", Tags = ["SMS"],
        Summary = "Confirm message sent",
        Description = "Confirms that an SMS message has been sent successfully",
        ExcludeFromOpenApi = true,
        ActorRequirement = ActorRequirement.None,
        TenantAccessMode = TenantAccessMode.ServiceTargetTenant,
        RequiredServiceScopes = [XFrameworkServiceScopes.SmsGatewayAgent, XFrameworkServiceScopes.TenantTarget],
        AllowedServiceCallers = [XFrameworkServiceNames.SmsGateway])]
    public static async Task<Result<CmdResponse>> Handle(
        ConfirmMessageSentRequest request,
        ISmsService smsService,
        CancellationToken ct)
    {
        return await smsService.ConfirmMessageSentAsync(request.Id, ct);
    }
}

public sealed record ConfirmMessageSentRequest : RequestBase
{
    public Guid Id { get; set; }
}
