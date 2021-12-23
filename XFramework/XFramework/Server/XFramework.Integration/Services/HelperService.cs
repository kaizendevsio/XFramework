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
        
        public string GenerateRandomString(long size)
        {
            var b = new byte[size];
            new RNGCryptoServiceProvider().GetBytes(b);
            return Encoding.ASCII.GetString(b);
        }
        public long GenerateRandomNumber(int start, int end)
        {
            var rnd = new Random();
            var n  = rnd.Next(start, end);
            return n;
        }
        
        public long GenerateRandomNumber(long min, long max)
        {
            var rand = new Random();
            byte[] buf = new byte[8];
            rand.NextBytes(buf);
            long longRand = BitConverter.ToInt64(buf, 0);

            return (Math.Abs(longRand % (max - min)) + min);
        }

        public string GenerateRandomString(int size)
        {
            throw new NotImplementedException();
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