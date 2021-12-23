using System;
using System.Collections.Generic;

namespace IdentityServer.Domain.Generic.Contracts.Responses
{
    public class AuthorizeIdentityContract
    {
        public Guid Uid { get; set; }
        public Guid Cuid { get; set; }
        public string AccessToken  { get; set; }
        public string RefreshToken  { get; set; }

        public List<IdentityRoleContract> RoleList { get; set; }
    }
}