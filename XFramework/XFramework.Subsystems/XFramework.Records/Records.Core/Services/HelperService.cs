using Records.Core.Interfaces;
using System.Security.Cryptography;
using System.Text;

namespace Records.Core.Services
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