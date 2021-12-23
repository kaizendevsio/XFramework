using System;

namespace IdentityServer.Domain.Generic.Contracts.Responses
{
    public class IdentityContactEntityContract
    {
        public long Id { get; set; }
        public bool IsEnabled { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsDeleted { get; set; }
        public string Name { get; set; }
    }
}