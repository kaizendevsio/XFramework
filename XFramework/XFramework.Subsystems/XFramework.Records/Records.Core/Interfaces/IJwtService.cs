using IdentityServer.Domain.BusinessObjects;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Records.Core.Interfaces
{
    public interface IJwtService
    {
        public Task<JwtTokenBO> GenerateToken(string username, Guid cuid);
        public Task<JwtTokenBO> GenerateToken(List<Claim> claims);
        public Task<JwtTokenBO> Refresh(string refreshToken, string accessToken, DateTime now);
        public Task<(ClaimsPrincipal, JwtSecurityToken)> DecodeJwtToken(string token);
    }
}