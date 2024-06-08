using Microsoft.AspNetCore.Mvc;
using SmsGateway.Core.Commands.Sms;
using SmsGateway.Core.Interfaces;
using SmsGateway.Domain.Shared.Contracts.Responses.Sms;

namespace SmsGateway.Api.Controllers.V1;

[ApiController]
[Route("api/[controller]")]
public class SmsGatewayNodeController : ControllerBase
{
    private readonly ICachingService _cachingService;
    private readonly IMediator _mediator;

    public SmsGatewayNodeController(ICachingService cachingService, IMediator mediator)
    {
        _cachingService = cachingService;
        _mediator = mediator;
    }
        
    [HttpGet("List/{agentClusterId}")]
    public async Task<IEnumerable<SmsNodeJob>> List([FromRoute] Guid agentClusterId)
    {
        var itemList = _cachingService.PendingMessageList
            .Where(x => x.Value.AgentClusterId == agentClusterId)
            .Where(x => x.Value.Status is MessageStatus.Queued)
            .Select(i => i.Value)
            .ToList();

        foreach (var current in itemList)
        {
            current.Status = MessageStatus.Processing;
        }

        return itemList;
    }
        
    [HttpPatch("MessageSent")]
    public async Task<IActionResult> MessageSent([FromQuery] Guid guid)
    {
        _ = _mediator.Send(new ConfirmSmsMessageSentRequest(guid));
        return Accepted();
    }
    
    [HttpGet("MessageReceived/{agentClusterId}")]
    public async Task<IActionResult> MessageReceived([FromRoute] Guid agentClusterId, [FromQuery] string sender, [FromQuery]string message, [FromQuery]string subscriptionId, [FromQuery]string receivedAt)
    {
        _ = _mediator.Send(new CreateMessageReceived()
        {
            Sender = sender,
            Message = message,
            AgentClusterId = agentClusterId,
            SubscriptionId = subscriptionId,
            ReceivedAt = receivedAt
        });
        return Accepted();
    }
}