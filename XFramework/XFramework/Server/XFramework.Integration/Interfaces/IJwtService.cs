using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using IdentityServer.Domain.Generic.Enums;
using XFramework.Domain.Generic.Interfaces;

namespace XFramework.Integration.Interfaces
{
    public interface IJwtService : IXFrameworkService
    {
        public Task<JwtTokenBO> GenerateToken(string username, Guid cuid, List<RoleEntity> roleEntity);
        public Task<JwtTokenBO> GenerateToken(List<Claim> claims);
        public Task<JwtTokenBO> Refresh(string refreshToken, string accessToken, DateTime now);
        public Task<(ClaimsPrincipal, JwtSecurityToken)> DecodeJwtToken(string token);
    }
}