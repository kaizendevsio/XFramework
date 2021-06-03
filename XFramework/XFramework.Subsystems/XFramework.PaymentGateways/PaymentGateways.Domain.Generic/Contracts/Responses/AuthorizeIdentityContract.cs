using System;

namespace PaymentGateways.Domain.Generic.Contracts.Responses
{
    public class AuthorizeIdentityContract
    {
        public Guid Uid { get; set; }
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
    }
}