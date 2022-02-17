using System;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using IdentityServer.Domain.Generic.Contracts.Requests;
using IdentityServer.Domain.Generic.Contracts.Requests.Create;
using MessagePack;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using StreamFlow.Domain.Generic.Enums;
using XFramework.Integration.Interfaces.Wrappers;

namespace IdentityServer.Test;

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
                await StreamFlowWrapper.PushAsync(new(new CreateIdentityRequest())
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