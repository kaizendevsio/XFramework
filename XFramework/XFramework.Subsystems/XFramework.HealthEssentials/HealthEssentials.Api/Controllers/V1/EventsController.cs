using HealthEssentials.Domain.Generics.Contracts.Responses.Consultation;
using HealthEssentials.Domain.Generics.Strings;
using Microsoft.AspNetCore.Mvc;
using XFramework.Integration.Interfaces.Wrappers;

namespace HealthEssentials.Api.Controllers.V1;


[Route("Api/V1/[controller]/[action]")]
[ApiController]
public class EventsController : XFrameworkControllerBase
{
    private readonly IMessageBusWrapper _messageBusWrapper;

    public EventsController(IMessageBusWrapper messageBusWrapper)
    {
        _messageBusWrapper = messageBusWrapper;
    }

    [HttpPost]
    public async Task<ActionResult> PublishEvent([FromBody] object payload ,string eventName, string topic)
    {
        await _messageBusWrapper.PublishAsync(eventName, topic, payload);
        return Ok("Event published");
    }
}