using SmsGateway.Api.Services;
using SmsGateway.Domain.Shared.Contracts.Requests.Create;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace SmsGateway.Api.Features.Sms.CreateReceived;

public static class CreateMessageReceivedEndpoint
{
    [MapPost("/api/sms/messages/received", Tags = ["SMS"],
        Summary = "Create received message record",
        Description = "Creates a record of a received SMS message",
        ExcludeFromOpenApi = true)]
    public static async Task<Result<CmdResponse>> Handle(
        CreateMessageReceivedRequest request,
        ISmsService smsService,
        CancellationToken ct)
    {
        return await smsService.CreateMessageReceivedAsync(request, ct);
    }
}
