using IdentityServer.Domain.BusinessObjects;
using Records.Core.DataAccess.Query.Entity.Identity.Authorization;
using System;
using System.IdentityModel.Tokens.Jwt;

namespace Records.Core.Interfaces
{
    public interface IHelperService
    {
        public string GenerateRandomString(int size);
    }
}