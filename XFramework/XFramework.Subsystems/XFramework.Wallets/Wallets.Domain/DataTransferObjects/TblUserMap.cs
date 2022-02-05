using System;
using System.Collections.Generic;

namespace Wallets.Domain.DataTransferObjects
{
    public partial class TblUserMap
    {
        public TblUserMap()
        {
            InverseSponsorUser = new HashSet<TblUserMap>();
            InverseUplineUser = new HashSet<TblUserMap>();
            TblUserBinaryListMultiplices = new HashSet<TblUserBinaryListMultiplex>();
            TblUserBinaryListSourceUserMaps = new HashSet<TblUserBinaryList>();
            TblUserBinaryListTargetUserMaps = new HashSet<TblUserBinaryList>();
            TblUserIncomeTransactionPairMaps = new HashSet<TblUserIncomeTransaction>();
            TblUserIncomeTransactionSourceMaps = new HashSet<TblUserIncomeTransaction>();
            TblUserIncomeTransactionTargetMaps = new HashSet<TblUserIncomeTransaction>();
        }

        public long Id { get; set; }
        public bool? IsEnabled { get; set; }
        public DateTime? CreatedAt { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public long? ModifiedBy { get; set; }
        public DateTime? LastChanged { get; set; }
        public string Uid { get; set; }
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

        public virtual TblUserBusinessPackage IdNavigation { get; set; }
        public virtual TblUserMap SponsorUser { get; set; }
        public virtual TblUserMap UplineUser { get; set; }
        public virtual ICollection<TblUserMap> InverseSponsorUser { get; set; }
        public virtual ICollection<TblUserMap> InverseUplineUser { get; set; }
        public virtual ICollection<TblUserBinaryListMultiplex> TblUserBinaryListMultiplices { get; set; }
        public virtual ICollection<TblUserBinaryList> TblUserBinaryListSourceUserMaps { get; set; }
        public virtual ICollection<TblUserBinaryList> TblUserBinaryListTargetUserMaps { get; set; }
        public virtual ICollection<TblUserIncomeTransaction> TblUserIncomeTransactionPairMaps { get; set; }
        public virtual ICollection<TblUserIncomeTransaction> TblUserIncomeTransactionSourceMaps { get; set; }
        public virtual ICollection<TblUserIncomeTransaction> TblUserIncomeTransactionTargetMaps { get; set; }
    }
}
