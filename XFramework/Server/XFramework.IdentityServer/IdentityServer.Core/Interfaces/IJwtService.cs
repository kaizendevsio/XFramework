using System;
using System.IdentityModel.Tokens.Jwt;
using System.Threading.Tasks;
using IdentityServer.Domain.BusinessObjects;

namespace IdentityServer.Core.Interfaces
{
    public interface IJwtService
    {
        public Task<JwtTokenBO> GenerateToken(string username, Guid cuid, JwtOptionsBO jwtOptions);
    }
}