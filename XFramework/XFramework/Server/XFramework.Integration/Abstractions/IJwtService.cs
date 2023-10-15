using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using IdentityServer.Domain.Generic.Enums;

namespace XFramework.Integration.Abstractions;

public interface IJwtService : IXFrameworkService
{
    public Task<JwtToken> GenerateToken(string username, Guid cuid, List<RoleEntity> roleEntity);
    public Task<JwtToken> GenerateToken(List<Claim> claims);
    public Task<JwtToken> Refresh(string refreshToken, string accessToken, DateTime now);
    public Task<(ClaimsPrincipal, JwtSecurityToken)> DecodeJwtToken(string token);
}