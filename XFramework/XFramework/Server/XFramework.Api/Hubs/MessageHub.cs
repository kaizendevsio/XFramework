using MediatR;
using XFramework.Core.Interfaces;
using XFramework.Integration.Security;

namespace XFramework.Api.Hubs
{
    public class MessageHub : HubBase
    {
        private readonly string _encryptionSecret;
        
        public MessageHub(IMediator mediator, ICachingService cachingService, IConfiguration configuration)
        {
            Mediator = mediator;
            Configuration = configuration;
            CachingService = cachingService;
            
            _encryptionSecret = Configuration.GetValue<string>("Application:EncryptionSecret");
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