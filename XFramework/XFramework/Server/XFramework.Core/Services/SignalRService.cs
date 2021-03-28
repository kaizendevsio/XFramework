using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using XFramework.Core.Interfaces;

namespace XFramework.Core.Services
{
    public class SignalRService : ISignalRService
    {
        public HubConnection Connection { get; set; }
        public static List<string> ConnectedUserIds = new List<string>();  

        public SignalRService()
        {
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            var connectionUrl = environment switch
            {
                "Development" => "https://8f9c1ba23f35.ngrok.io/hubs/stream",
                "Staging" => "https://8f9c1ba23f35.ngrok.io/hubs/stream",
                "Production" => "https://8f9c1ba23f35.ngrok.io/hubs/stream",
                _ => "https://8f9c1ba23f35.ngrok.io/hubs/stream"
            };

            Connection = new HubConnectionBuilder()
                .WithUrl(connectionUrl)
               // .WithAutomaticReconnect()
                .Build();
            Task.Run(StartListenService);
        }

        public async Task StartListenService()
        {
            Connection.On<string, string>("ReceiveMessage", (intent, message) =>
            {
                Debug.WriteLine($"Message Received ({DateTime.Now}): [{intent}] {message}");
            });
            
            Connection.Reconnecting += error =>
            {
                Debug.Assert(Connection.State == HubConnectionState.Reconnecting);

                // Notify users the connection was lost and the client is reconnecting.
                // Start queuing or dropping messages.
                Debug.WriteLine("Connection to server hub lost, trying to reconnect..");
                return Task.CompletedTask;
            };
            
            Connection.Reconnected += connectionId =>
            {
                Debug.Assert(Connection.State == HubConnectionState.Connected);

                // Notify users the connection was reestablished.
                // Start dequeuing messages queued while reconnecting if any.
                Debug.WriteLine("Connection to server hub restored");
                return Task.CompletedTask;
            };
            
            Connection.Closed += connectionId =>
            {
                Debug.WriteLine("Connection to server hub closed");
                Connection.StartAsync();
                return Task.CompletedTask;
            };

            Debug.WriteLine("Connecting to server hub..");
            try
            {
                await Connection.StartAsync();
                Debug.WriteLine("Connected to server hub.");
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
            }
        }

        public async Task<T> InvokeAsync<T>(string methodName, string args1)
        {
            int retry = 0;
            
            RetryConnection:
            if (Connection.State != HubConnectionState.Connected)
            {
                try
                {
                    await Connection.StartAsync();
                }
                catch (Exception e)
                {
                    //  throw new ArgumentException(e.Message);
                    if (retry > 3)
                    {
                        throw new ArgumentException(e.Message);    
                    }
                }

                retry++;
                if (retry < 3)
                {
                    await Task.Delay(1500);
                    goto RetryConnection;
                }

            }
           
            return await Connection.InvokeAsync<T>(methodName, args1);
            
        }
    }
}