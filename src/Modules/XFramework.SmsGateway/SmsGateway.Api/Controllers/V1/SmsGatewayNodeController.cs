using Microsoft.AspNetCore.Mvc;
using SmsGateway.Core.Interfaces;
using SmsGateway.Domain.Shared.Contracts.Requests.Update;
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
    public async Task<List<SmsNodeResponse>> List([FromRoute] Guid agentClusterId)
    {
        var itemList = _cachingService.PendingMessageList
            .Where(x => x.AgentClusterId == agentClusterId)
            .Select(i => new SmsNodeResponse
            {
                Id = i.Id,
                Recipient = i.Recipient,
                Message = i.Message
            })
            .ToList();
        _cachingService.PendingMessageList.RemoveAll(x => itemList.Any(i => i.Id == x.Id));

        return itemList;
    }
        
    [HttpPatch("MessageSent")]
    public async Task<IActionResult> MessageSent(Guid guid)
    {
        Task.Run(async () => await _mediator.Send(new ConfirmSmsMessageSentRequest(guid)));
        return Accepted();
    }
}