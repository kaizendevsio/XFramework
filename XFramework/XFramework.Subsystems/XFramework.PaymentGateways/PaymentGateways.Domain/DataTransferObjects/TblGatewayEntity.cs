using System;
using System.Collections.Generic;

#nullable disable

namespace PaymentGateways.Domain.DataTransferObjects
{
    public partial class TblGatewayEntity
    {
        public TblGatewayEntity()
        {
            TblGatewayCategories = new HashSet<TblGatewayCategory>();
            TblGatewayEndpoints = new HashSet<TblGatewayEndpoint>();
        }

        public long Id { get; set; }
        public string Uuid { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public DateTime? CreatedAt { get; set; }
        public bool? IsEnabled { get; set; }
        public bool? IsDeleted { get; set; }
        public DateTime? ModifiedOn { get; set; }
        public DateTime? ModifiedAt { get; set; }

        public virtual ICollection<TblGatewayCategory> TblGatewayCategories { get; set; }
        public virtual ICollection<TblGatewayEndpoint> TblGatewayEndpoints { get; set; }
    }
}
