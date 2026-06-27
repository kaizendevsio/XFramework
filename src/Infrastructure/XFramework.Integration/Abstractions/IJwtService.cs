using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace XFramework.Integration.Abstractions;

public interface IJwtService : IXFrameworkService
{
    public Task<JwtToken> GenerateToken(string username, Guid id, List<Guid> roleTypes, Guid? tenantId = null);
    public Task<JwtToken> GenerateToken(List<Claim> claims);
    public Task<JwtToken> Refresh(string refreshToken, string accessToken, DateTime now);
    public Task<(ClaimsPrincipal, JwtSecurityToken)> DecodeJwtToken(string token);
    public Task<(ClaimsPrincipal, JwtSecurityToken)> DecodeExpiredToken(string token);
}
