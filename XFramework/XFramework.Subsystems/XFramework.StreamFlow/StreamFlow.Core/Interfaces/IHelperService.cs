using IdentityServer.Domain.BusinessObjects;
using StreamFlow.Core.DataAccess.Query.Entity.Identity.Authorization;
using System;
using System.IdentityModel.Tokens.Jwt;

namespace StreamFlow.Core.Interfaces
{
    public interface IHelperService
    {
        public string GenerateRandomString(int size);
    }
}