using SmsGateway.Api.Features.Sms.ConfirmSent;
using SmsGateway.Api.Features.Sms.Create;
using SmsGateway.Api.Features.Sms.CreateReceived;
using SmsGateway.Api.Features.Sms.GetPending;
using SmsGateway.Api.Features.Sms.GetPendingWithStatus;
using SmsGateway.Api.Features.Sms.GetScheduled;

namespace SmsGateway.Api.Features.Sms;

/// <summary>
/// Extension methods for registering SMS endpoints
/// </summary>
public static class SmsEndpoints
{
    /// <summary>
    /// Maps all SMS endpoints to the application
    /// </summary>
    public static IEndpointRouteBuilder MapSmsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/sms")
            .WithTags("SMS")
            .WithOpenApi();

        // Map individual endpoints
        app.MapCreateSmsMessage();
        app.MapConfirmMessageSent();
        app.MapCreateMessageReceived();
        app.MapGetPendingSmsMessages();
        app.MapGetScheduledSmsMessages();
        app.MapGetPendingWithStatusUpdate();

        return app;
    }
}