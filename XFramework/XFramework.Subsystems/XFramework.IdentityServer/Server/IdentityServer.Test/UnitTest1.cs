using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using XFramework.Domain.Generic.Configurations;
using XFramework.Integration.Drivers;
using XFramework.Integration.Interfaces;
using XFramework.Integration.Services;
using XFramework.Integration.Wrappers;

namespace IdentityServer.Test
{
    [TestClass]
    public class UnitTest1
    {
        public UnitTest1()
        {
            StreamFlowWrapper = new StreamFlowDriverSignalR(
                new SignalRService(
                    new StreamFlowConfiguration
                    {
                        ServerUrls = new()
                        {
                            new("https://localhost:6001/hubs/v1/messageQueue/stream")
                        },
                        ReconnectDelay = 1500,
                        MaxRetry = 3,
                        ClientGuid = new Guid("3902761a-822d-4c6b-8e2d-323fd501bcd6")
                    }
                ));
        }

        private IMessageBusWrapper StreamFlowWrapper { get; set; }
        
        [TestMethod]
        public async Task TestMethod1()
        {
            var startTime = DateTime.Now;
            Debug.WriteLine($"Start Time: {startTime}");

            if (await StreamFlowWrapper.Connect())
            {
                for (var i = 0; i < 20; i++)
                {
                    await StreamFlowWrapper.Push(new()
                    {
                        MethodName = "Ping",
                        Message = "Hello",
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