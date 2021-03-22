using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using IdentityServer.Core.DataAccess.Query.Entity.Identity.Authorization;
using IdentityServer.Core.Interfaces;
using IdentityServer.Domain.BusinessObjects;
using Microsoft.IdentityModel.Tokens;

namespace IdentityServer.Core.Services
{
    public class JwtService : IJwtService
    {
        public virtual async Task<JwtTokenBO> GenerateToken(string username, Guid cuid, JwtOptionsBO jwtOptions)
        {
            var authClaims = new List<Claim>  
            {  
                new (JwtRegisteredClaimNames.GivenName, username),
                new (JwtRegisteredClaimNames.UniqueName, cuid.ToString()),
                new (JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new (JwtRegisteredClaimNames.AuthTime, DateTime.UtcNow.ToString())
            };

            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Secret));
            
            var token = new JwtSecurityToken(  
                issuer: jwtOptions.ValidIssuer,  
                audience: jwtOptions.ValidAudience,
                expires: DateTime.Now.AddMinutes(DateTime.Parse(jwtOptions.AccessTokenLifespan).Minute),
                claims: authClaims,  
                signingCredentials: new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha512)  
            );
            
            var refreshToken = new RefreshTokenBO
            {
                Cuid = cuid,
                Token = GenerateRefreshToken(),
                ExpireAt = DateTime.UtcNow.AddMinutes(DateTime.Parse(jwtOptions.RefreshTokenLifespan).Minute)
            };

            return new()
            {
                AccessToken = new JwtSecurityTokenHandler().WriteToken(token),
                RefreshToken = refreshToken.Token
            };
        }
        
        private static string GenerateRefreshToken()
        {
            var randomNumber = new byte[32];
            using var randomNumberGenerator = RandomNumberGenerator.Create();
            randomNumberGenerator.GetBytes(randomNumber);
            return Convert.ToBase64String(randomNumber);
        }
    }
}