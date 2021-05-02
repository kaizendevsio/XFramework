using System.Security.Cryptography;
using System.Text;
using XFramework.Integration.Interfaces;
using XFramework.Integration.Services.Helpers;

namespace XFramework.Integration.Services
{
    public class HelperService : IHelperService
    {
        public HelperService()
        {
        }
        
        public string GenerateRandomString(int size)
        {
            var b = new byte[size];
            new RNGCryptoServiceProvider().GetBytes(b);
            return Encoding.ASCII.GetString(b);
        }

        public HttpHelper Http { get; set; } = new();
        public StopWatchHelper StopWatch { get; set; } = new();
    }
}