using System;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using IdentityServer.Domain.Generic.Contracts.Requests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using StreamFlow.Domain.Enums;
using XFramework.Domain.Generic.Configurations;
using XFramework.Domain.Generic.Contracts.Requests;
using XFramework.Integration.Drivers;
using XFramework.Integration.Interfaces;
using XFramework.Integration.Interfaces.Wrappers;
using XFramework.Integration.Services;
using XFramework.Integration.Services.Helpers;

namespace IdentityServer.Test
{
    [TestClass]
    public class UnitTest1
    {
        public UnitTest1()
        {
            /*StreamFlowWrapper = new StreamFlowDriverSignalR(
                new SignalRService(
                    new StreamFlowConfiguration
                    {
                        ServerUrls = new()
                        {
                            new("https://localhost:6001/hubs/v1/messageQueue/stream")
                        },
                        ReconnectDelay = 1500,
                        MaxRetry = 3,
                        ClientGuid = new Guid("3902761a-822d-4c6b-8e2d-323fd501bcd6"),
                        ClientName = "Unit Test"
                    },
                    new()
                ));*/
        }

        private IMessageBusWrapper StreamFlowWrapper { get; set; }
        
        [TestMethod]
        public async Task TestMethod1()
        {
            var startTime = DateTime.Now;
            Debug.WriteLine($"Start Time: {startTime}");

            if (await StreamFlowWrapper.Connect())
            {
                for (var i = 0; i < 1; i++)
                {
                    await StreamFlowWrapper.Push(new(new CreateIdentityRequest())
                    {
                        Message = "Hello fucking world",
                        ExchangeType = MessageExchangeType.Direct,
                        Recipient = new Guid("3902761a-822d-4c6b-8e2d-323fd501bcd6"),
                        Data = JsonSerializer.Serialize(new
                        {
                            Table = "",
                            Fucker = "hehehe",
                            Re = 123.43
                        })
                    });
                    
                    Thread.Sleep(1000); 
                }
            }

            var endTime = DateTime.Now;
            var elapsedTime = endTime - startTime;
            Debug.WriteLine($"End Time: {endTime}");
            Debug.WriteLine($"Elapsed Time: {elapsedTime}");
        }
    }
}