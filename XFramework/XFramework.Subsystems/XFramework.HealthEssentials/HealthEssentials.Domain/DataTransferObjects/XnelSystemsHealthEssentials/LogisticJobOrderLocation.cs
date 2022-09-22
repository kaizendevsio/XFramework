using System;
using System.Collections.Generic;

namespace HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials
{
    public partial class LogisticJobOrderLocation
    {
        public long Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        public bool? IsEnabled { get; set; }
        public bool IsDeleted { get; set; }
        public long LogisticJobOrderId { get; set; }
        public short Status { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? UnitNumber { get; set; }
        public string? Street { get; set; }
        public string? Building { get; set; }
        public string? BarangayGuid { get; set; }
        public string? CityGuid { get; set; }
        public string? Subdivision { get; set; }
        public string? RegionGuid { get; set; }
        public bool? MainAddress { get; set; }
        public string? ProvinceGuid { get; set; }
        public string? CountryGuid { get; set; }
        public string Guid { get; set; } = null!;
        public short Priority { get; set; }
        public bool IsDestination { get; set; }
        public string? ClientGuid { get; set; }
        public string? ClientName { get; set; }

        public virtual LogisticJobOrder LogisticJobOrder { get; set; } = null!;
    }
}
