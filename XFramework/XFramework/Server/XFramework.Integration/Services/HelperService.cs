using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using XFramework.Integration.Interfaces;
using XFramework.Integration.Services.Helpers;

namespace XFramework.Integration.Services
{
    public class HelperService : IHelperService
    {
        public HttpHelper Http { get; }

        public HelperService(IConfiguration configuration)
        {
            Http = new HttpHelper(configuration);
        }
        
        public string GenerateRandomString(int size)
        {
            var b = new byte[size];
            new RNGCryptoServiceProvider().GetBytes(b);
            return Encoding.ASCII.GetString(b);
        }
        public string GenerateReferenceString()
        {
            var ticks = new DateTime(2021,1,1).Ticks;
            var ans = DateTime.Now.Ticks - ticks;
            var uniqueId = ans.ToString("x").ToUpper();
            return uniqueId;
        }

        public StopWatchHelper StopWatch { get; set; } = new();
    }
}