using Microsoft.IdentityModel.Tokens;
using StreamFlow.Core.DataAccess.Query.Entity.Identity.Authorization;
using StreamFlow.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace StreamFlow.Core.Services
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