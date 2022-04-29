using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmsGateway.Core.DataAccess.Commands.Entity.Sms;
using SmsGateway.Core.Interfaces;
using SmsGateway.Domain.Generic.Contracts.Responses.Sms;

namespace SmsGateway.Api.Controllers.V1
{
    [Route("api/[controller]")]
    [ApiController]
    public class SmsGatewayNodeController : XFrameworkControllerBase
    {
        private readonly ICachingService _cachingService;

        public SmsGatewayNodeController(ICachingService cachingService)
        {
            _cachingService = cachingService;
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
        public async Task<IActionResult> MessageSent(Guid? guid)
        {
            Task.Run(async () => await _mediator.Send(new ConfirmSmsMessageSentCmd()));
            return Accepted();
        }
    }
}
