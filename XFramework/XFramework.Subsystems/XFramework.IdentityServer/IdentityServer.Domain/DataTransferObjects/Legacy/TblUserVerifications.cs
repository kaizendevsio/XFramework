using System;

namespace IdentityServer.Domain.DataTransferObjects.Legacy
{
    public partial class TblUserVerifications
    {
        public long Id { get; set; }
        public bool? IsEnabled { get; set; }
        public DateTime? CreatedOn { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? ModifiedOn { get; set; }
        public long? ModifiedBy { get; set; }
        public DateTime? LastChanged { get; set; }
        public long? UserAuthId { get; set; }
        public long? VerificationTypeId { get; set; }
        public short? Status { get; set; }
        public DateTimeOffset? StatusUpdatedOn { get; set; }
        public string Token { get; set; }
        public DateTime? Expiry { get; set; }

        public virtual TblUserAuth UserAuth { get; set; }
        public virtual TblVerificationType VerificationType { get; set; }
    }
}
