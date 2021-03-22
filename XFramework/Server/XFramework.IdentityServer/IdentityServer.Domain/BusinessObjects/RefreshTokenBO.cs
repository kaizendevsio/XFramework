using System;

namespace IdentityServer.Domain.BusinessObjects
{
    public class RefreshTokenBO
    {
        public string Token { get; set; }
        public DateTime ExpireAt { get; set; }
        public Guid Cuid { get; set; }
    }
}