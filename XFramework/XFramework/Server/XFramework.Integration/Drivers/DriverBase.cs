using Microsoft.Extensions.Configuration;

namespace XFramework.Integration.Drivers
{
    public class DriverBase
    {
        protected IConfiguration Configuration { get; set; }
    }
}