using HealthEssentials.Domain.Generics.Contracts.Responses.Consultation;
using HealthEssentials.Domain.Generics.Strings;
using Microsoft.AspNetCore.Mvc;
using XFramework.Integration.Interfaces.Wrappers;

namespace HealthEssentials.Api;

[ApiController]
[Route("api/[controller]/[action]")]
public class TestController : ControllerBase
{
    private readonly IMessageBusWrapper _messageBusWrapper;

    public TestController(IMessageBusWrapper messageBusWrapper)
    {
        _messageBusWrapper = messageBusWrapper;
    }
    
    // GET
    [HttpGet]
    public IActionResult Index()
    {
        _messageBusWrapper.PublishAsync(HealthEssentialsEvent.CommenceConsultation, Guid.Parse("254606f0-4983-442b-bcf1-89b6d299e4ce"), new ConsultationJobOrderResponse(){Remarks = "Test Remarks"});
        return Ok();
    }
}