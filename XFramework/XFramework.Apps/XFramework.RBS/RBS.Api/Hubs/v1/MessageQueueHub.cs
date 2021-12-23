using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Mapster;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using RBS.Core.DataAccess.Commands.Entity.Game;
using RBS.Core.DataAccess.Commands.Entity.Game.User;
using RBS.Core.DataAccess.Query.Entity.Wallet.User;
using RBS.Core.Interfaces;
using RBS.Domain.Contracts.Requests;
using RBS.Domain.Contracts.Responses;
using RBS.Domain.Enums;

namespace RBS.Api.Hubs.v1
{
    public class MessageQueueHub : HubBase
    {
        public bool IsStarted { 
            get => _cachingService.IsStarted;
            set => _cachingService.IsStarted = value;
        }
        public decimal Wala { 
            get => _cachingService.Wala;
            set => _cachingService.Wala = value;
        }
        public decimal Meron { 
            get => _cachingService.Meron;
            set => _cachingService.Meron = value;
        }
        
        public MessageQueueHub(IMediator mediator, ICachingService cachingService) 
        {
            _cachingService = cachingService;
            _mediator = mediator;
        }
        public override async Task OnConnectedAsync()
        {
            Console.WriteLine($"New Connection Detected with ID {Context.ConnectionId}");
            await base.OnConnectedAsync();
            await StartTick();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            await base.OnDisconnectedAsync(exception);
            Console.WriteLine($"Connection Lost and Unregistered with ID {Context.ConnectionId}");
        }

        private async Task StartTick()
        {
            if (IsStarted) return;
            
            restart:
            IsStarted = true;
            
            await _mediator.Send(new NewGameCmd());

            var rnd = new Random();
            var startTime = DateTime.UtcNow;
            Wala = 0;
            Meron = 0;

            await Clients.All.SendAsync("UpdateBetStatus", true);
            while ((DateTime.UtcNow - startTime).TotalSeconds < 30)
            {
                Meron += rnd.Next(1000);
                Wala += rnd.Next(1000);

                await Clients.All.SendAsync("UpdateTally", Meron, Wala,(DateTime.UtcNow - startTime).TotalSeconds);
                Thread.Sleep(100);
            }
            await Clients.All.SendAsync("UpdateBetStatus", false);
            
            startTime = DateTime.UtcNow;
            while ((DateTime.UtcNow - startTime).TotalSeconds < 30)
            {
                Meron += rnd.Next(1000);
                Wala += rnd.Next(1000);

                await Clients.All.SendAsync("WaitTally",(DateTime.UtcNow - startTime).TotalSeconds);
                Thread.Sleep(100);
            }

            var winner = (BetOption) rnd.Next(2);
            await _mediator.Send(new CheckWinnerCmd(){BetOption = winner});
            await Clients.All.SendAsync("Message", $"{winner} Wins!");

            goto restart;
            IsStarted = false;
        }

        public async Task<bool> Bet(SubmitBetRequest request)
        {
            Console.WriteLine($"Bet Submitted by {Context.ConnectionId}");
            var result = await _mediator.Send(request.Adapt<GameUserBetCmd>());
            return new();
        }
        
        public async Task<WalletDetailsContract> GetWallet(WalletDetailsQuery request)
        {
            var result = await _mediator.Send(request.Adapt<GameUserBetCmd>());
            return new();
        }
       
    }
}