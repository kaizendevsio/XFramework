using IdentityServer.Domain.BusinessObjects;
using Microsoft.IdentityModel.Tokens;
using Records.Core.DataAccess.Query.Entity.Identity.Authorization;
using Records.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
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