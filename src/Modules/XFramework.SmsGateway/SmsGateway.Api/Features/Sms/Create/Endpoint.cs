using SmsGateway.Api.Services;
using SmsGateway.Domain.Shared.Contracts.Requests.Create;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace SmsGateway.Api.Features.Sms.Create;

public static class CreateSmsMessageEndpoint
{
    [StreamFlowHandler]
    [MapPost("/api/sms/messages", Tags = ["SMS"],
        Summary = "Create a new SMS message",
        Description = "Creates a new SMS message to be sent via the SMS gateway",
        ExcludeFromOpenApi = true)]
    public static async Task<Result<CmdResponse>> Handle(
        CreateSmsMessageRequest request,
        ISmsService smsService,
        CancellationToken ct)
    {
        return await smsService.CreateSmsMessageAsync(request, ct);
    }
}
