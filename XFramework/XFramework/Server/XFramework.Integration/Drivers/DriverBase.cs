using System;
using Microsoft.Extensions.Configuration;
using XFramework.Integration.Interfaces.Wrappers;

namespace XFramework.Integration.Drivers
{
    public class DriverBase
    {
        protected IConfiguration Configuration { get; set; }
        public IMessageBusWrapper StreamFlowDriver { get; set; }
        public Guid? TargetClient { get; set; }
    }
}