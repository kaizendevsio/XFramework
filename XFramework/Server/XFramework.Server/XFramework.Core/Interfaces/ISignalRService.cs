using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;

namespace XFramework.Core.Interfaces
{
    public interface ISignalRService
    {
        public HubConnection Connection { get; set; }
        public static List<string> ConnectedUserIds = new List<string>();
        public Task StartListenService();
        public Task<T> InvokeAsync<T>(string methodName, string args1);
    }
}