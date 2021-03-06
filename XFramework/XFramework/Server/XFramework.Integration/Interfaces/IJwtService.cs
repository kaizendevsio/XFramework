﻿using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading.Tasks;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Domain.Generic.Interfaces;

namespace XFramework.Integration.Interfaces
{
    public interface IJwtService : IXFrameworkService
    {
        public Task<JwtTokenBO> GenerateToken(string username, Guid cuid);
        public Task<JwtTokenBO> GenerateToken(List<Claim> claims);
        public Task<JwtTokenBO> Refresh(string refreshToken, string accessToken, DateTime now);
        public Task<(ClaimsPrincipal, JwtSecurityToken)> DecodeJwtToken(string token);
    }
}