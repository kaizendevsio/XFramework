using SmsGateway.Api.Services;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace SmsGateway.Api.Features.Sms.ConfirmSent;

public static class ConfirmMessageSentEndpoint
{
    [MapPatch("/api/sms/messages/{id:guid}/sent", Tags = ["SMS"],
        Summary = "Confirm message sent",
        Description = "Confirms that an SMS message has been sent successfully",
        ExcludeFromOpenApi = true)]
    public static async Task<Result<CmdResponse>> Handle(
        Guid id,
        ISmsService smsService,
        CancellationToken ct)
    {
        return await smsService.ConfirmMessageSentAsync(id, ct);
    }
}
