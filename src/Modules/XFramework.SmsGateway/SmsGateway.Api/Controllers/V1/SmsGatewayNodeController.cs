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
        
    [HttpGet("List")]
    public async Task<List<SmsNodeResponse>> List()
    {
        var itemList = _cachingService.PendingMessageList
            .Select(i => new SmsNodeResponse
            {
                Recipient = i.Recipient,
                Message = i.Message
            })
            .ToList();
        _cachingService.PendingMessageList.Clear();

        return itemList;
    }
        
    [HttpPatch("MessageSent")]
    public async Task<IActionResult> MessageSent(Guid guid)
    {
        Task.Run(async () => await _mediator.Send(new ConfirmSmsMessageSentRequest(guid)));
        return Accepted();
    }
}