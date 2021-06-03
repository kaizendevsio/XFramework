using System;

#nullable disable

namespace PaymentGateways.Domain.DataTransferObjects
{
    public partial class TblConfiguration
    {
        public long Id { get; set; }
        public long ApplicationId { get; set; }
        public string Key { get; set; }
        public string Value { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public bool IsDeleted { get; set; }

        public virtual TblApplication Application { get; set; }
    }
}
