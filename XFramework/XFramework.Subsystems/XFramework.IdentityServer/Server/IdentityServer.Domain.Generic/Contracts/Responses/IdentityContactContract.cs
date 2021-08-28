using System;

namespace IdentityServer.Domain.Generic.Contracts.Responses
{
    public class IdentityContactContract
    {
        public long Id { get; set; }
        public bool IsEnabled { get; set; }
        public DateTime CreatedAt { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public long? ModifiedBy { get; set; }
        public bool IsDeleted { get; set; }
        public long? UcentitiesId { get; set; }
        public string Value { get; set; }
        public long? UserCredentialId { get; set; }

        public virtual IdentityContactEntityContract Ucentities { get; set; }
    }
}