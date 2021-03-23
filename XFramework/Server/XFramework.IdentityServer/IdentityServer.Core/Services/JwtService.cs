using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
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
        private readonly JwtOptionsBO _jwtOptions;

        public JwtService(JwtOptionsBO jwtOptions)
        {
            _jwtOptions = jwtOptions;
        }
        public virtual async Task<JwtTokenBO> GenerateToken(string username, Guid cuid)
        {
            var authClaims = new List<Claim>  
            {  
                new (JwtRegisteredClaimNames.GivenName, username),
                new (JwtRegisteredClaimNames.UniqueName, cuid.ToString()),
                new (JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new (JwtRegisteredClaimNames.AuthTime, DateTime.UtcNow.ToString())
            };

            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.Secret));
            
            var token = new JwtSecurityToken(  
                issuer: _jwtOptions.ValidIssuer,  
                audience: _jwtOptions.ValidAudience,
                expires: DateTime.Now.AddMinutes(DateTime.Parse(_jwtOptions.AccessTokenLifespan).Minute),
                claims: authClaims,  
                signingCredentials: new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha512)  
            );
            
            var refreshToken = new RefreshTokenBO
            {
                Cuid = cuid,
                Token = GenerateRefreshToken(),
                ExpireAt = DateTime.UtcNow.AddMinutes(DateTime.Parse(_jwtOptions.RefreshTokenLifespan).Minute)
            };

            return new()
            {
                AccessToken = new JwtSecurityTokenHandler().WriteToken(token),
                RefreshToken = refreshToken.Token
            };
        }

        public virtual async Task<JwtTokenBO> GenerateToken(List<Claim> claims)
        {
            var authClaims = claims;

            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.Secret));
            
            var token = new JwtSecurityToken(  
                issuer: _jwtOptions.ValidIssuer,  
                audience: _jwtOptions.ValidAudience,
                expires: DateTime.Now.AddMinutes(DateTime.Parse(_jwtOptions.AccessTokenLifespan).Minute),
                claims: authClaims,  
                signingCredentials: new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha512)  
            );
            
            var refreshToken = new RefreshTokenBO
            {
                //Cuid = cuid,
                Token = GenerateRefreshToken(),
                ExpireAt = DateTime.UtcNow.AddMinutes(DateTime.Parse(_jwtOptions.RefreshTokenLifespan).Minute)
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
        
        public async Task<JwtTokenBO> Refresh(string refreshToken, string accessToken, DateTime now)
        {
            var (principal, jwtToken) = await DecodeJwtToken(accessToken);
            if (jwtToken == null || !jwtToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256Signature))
            {
                throw new SecurityTokenException("Invalid token");
            }

            var userName = principal.Identity.Name;
            /*if (!_usersRefreshTokens.TryGetValue(refreshToken, out var existingRefreshToken))
            {
                throw new SecurityTokenException("Invalid token");
            }
            if (existingRefreshToken.UserName != userName || existingRefreshToken.ExpireAt < now)
            {
                throw new SecurityTokenException("Invalid token");
            }*/

            return await GenerateToken(principal.Claims.ToList()); // need to recover the original claims
        }

        public async Task<(ClaimsPrincipal, JwtSecurityToken)> DecodeJwtToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new SecurityTokenException("Invalid token");
            }
            var principal = new JwtSecurityTokenHandler()
                .ValidateToken(token,
                    new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidAudience = _jwtOptions.ValidAudience,
                        ValidIssuer = _jwtOptions.ValidIssuer,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(_jwtOptions.Secret)),
                        RequireExpirationTime = false,
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.FromMinutes(1)
                    },
                    out var validatedToken);
            return (principal, validatedToken as JwtSecurityToken);
        }
    }
}