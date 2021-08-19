using RBS.Core.Interfaces;
using System.Security.Cryptography;
using System.Text;

namespace RBS.Core.Services
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





    }
}