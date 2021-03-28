using System;

namespace Records.Domain.Contracts
{
    public class GetApplicationListContract
    {
        public string AppName { get; set; }
        public string Description { get; set; }
        public string Uid { get; set; }
        public short? Status { get; set; }
        public DateTime? Expiration { get; set; }
        public DateTime? AvailabilityDate { get; set; }
        public long? ParentAppId { get; set; }
        public DateTime? CreatedOn { get; set; }
    }
}