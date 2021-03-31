using System;
using System.IdentityModel.Tokens.Jwt;

namespace StreamFlow.Core.Interfaces
{
    public interface IHelperService
    {
        public string GenerateRandomString(int size);
    }
}