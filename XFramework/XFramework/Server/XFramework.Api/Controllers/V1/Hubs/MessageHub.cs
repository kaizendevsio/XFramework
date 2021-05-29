using System;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Configuration;
using XFramework.Core.Interfaces;
using XFramework.Integration.Security;

namespace XFramework.Api.Controllers.V1.Hubs
{
    public class MessageHub : HubBase
    {
        private string _encryptionSecret;
        
        public MessageHub(IMediator mediator, ICachingService cachingService, IConfiguration configuration)
        {
            _mediator = mediator;
            _configuration = configuration;
            _cachingService = cachingService;
            
            _encryptionSecret = _configuration.GetValue<string>("Application:EncryptionSecret");
        }
        
        public override async Task OnConnectedAsync()
        {
            Console.WriteLine($"New Connection Detected with ID {Context.ConnectionId}");
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            await base.OnDisconnectedAsync(exception);
            Console.WriteLine($"Connection Lost and Unregistered with ID {Context.ConnectionId}");
        }

        
        public async Task<string> EncryptWithAes(string text)
        {
            return text.EncryptWithAes(_encryptionSecret);
        }
        
        public async Task<string> DecryptWithAes(string cipherText)
        {
            return cipherText.DecryptWithAes(_encryptionSecret);
        }
    }
}