using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmsGateway.Core.Interfaces;
using SmsGateway.Domain.Generic.Contracts.Responses.Sms;
using SmsGateway.Domain.Generic.Enums;

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
            var item = _cachingService.PendingMessageList.SingleOrDefault(i => i.Guid == $"{guid}");
            _cachingService.PendingMessageList.Remove(item);

            return Accepted();
        }
    }
}
