using System;
using System.Collections.Generic;

#nullable disable

namespace PaymentGateways.Domain.DataTransferObjects
{
    public partial class RegistryConfiguration
    {
        public long Id { get; set; }
        public long ApplicationId { get; set; }
        public string Key { get; set; }
        public string Value { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public bool IsDeleted { get; set; }
        public long? GroupId { get; set; }
        public string Unit { get; set; }
        public string Guid { get; set; }

        public virtual Application Application { get; set; }
        public virtual RegistryConfigurationGroup Group { get; set; }
    }
}
