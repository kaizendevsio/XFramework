using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using IdentityServer.Domain.Shared.Enums;

namespace XFramework.Integration.Abstractions;

public interface IJwtService : IXFrameworkService
{
    public Task<JwtToken> GenerateToken(string username, Guid id, List<Guid> roleTypes);
    public Task<JwtToken> GenerateToken(List<Claim> claims);
    public Task<JwtToken> Refresh(string refreshToken, string accessToken, DateTime now);
    public Task<(ClaimsPrincipal, JwtSecurityToken)> DecodeJwtToken(string token);
}