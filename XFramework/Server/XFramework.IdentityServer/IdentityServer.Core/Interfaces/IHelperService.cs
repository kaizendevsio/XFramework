using System;
using System.IdentityModel.Tokens.Jwt;
using IdentityServer.Core.DataAccess.Query.Entity.Identity.Authorization;
using IdentityServer.Domain.BusinessObjects;

namespace IdentityServer.Core.Interfaces
{
    public interface IHelperService
    {
        public string GenerateRandomString(int size);
    }
}