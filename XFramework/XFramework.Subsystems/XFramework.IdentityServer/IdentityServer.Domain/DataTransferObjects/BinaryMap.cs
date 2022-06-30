using System;
using System.Collections.Generic;

namespace IdentityServer.Domain.DataTransferObjects
{
    public partial class BinaryMap
    {
        public BinaryMap()
        {
            BinaryListMultiplices = new HashSet<BinaryListMultiplex>();
            BinaryListSourceUserMaps = new HashSet<BinaryList>();
            BinaryListTargetUserMaps = new HashSet<BinaryList>();
            InverseSponsorUser = new HashSet<BinaryMap>();
            InverseUplineUser = new HashSet<BinaryMap>();
        }

        public long Id { get; set; }
        public bool? IsEnabled { get; set; }
        public DateTime CreatedAt { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime ModifiedAt { get; set; }
        public long? ModifiedBy { get; set; }
        public DateTime? LastChanged { get; set; }
        public string Guid { get; set; }
        public long? SponsorUserId { get; set; }
        public long? UplineUserId { get; set; }
        public short? Position { get; set; }
        public long LeftLegCount { get; set; }
        public long RightLegCount { get; set; }
        public string Alias { get; set; }
        public long? Level { get; set; }
        public long? BinaryLeft { get; set; }
        public long? BinaryRight { get; set; }
        public int? LeftLegSilverCount { get; set; }
        public int? RightLegSilverCount { get; set; }
        public int? LeftLegGoldCount { get; set; }
        public int? RightLegGoldCount { get; set; }
        public int? UplineLevel { get; set; }

        public virtual BinaryMap SponsorUser { get; set; }
        public virtual BinaryMap UplineUser { get; set; }
        public virtual ICollection<BinaryListMultiplex> BinaryListMultiplices { get; set; }
        public virtual ICollection<BinaryList> BinaryListSourceUserMaps { get; set; }
        public virtual ICollection<BinaryList> BinaryListTargetUserMaps { get; set; }
        public virtual ICollection<BinaryMap> InverseSponsorUser { get; set; }
        public virtual ICollection<BinaryMap> InverseUplineUser { get; set; }
    }
}
