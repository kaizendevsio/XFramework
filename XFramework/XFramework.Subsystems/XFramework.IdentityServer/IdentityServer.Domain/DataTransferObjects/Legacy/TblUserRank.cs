using System;

namespace IdentityServer.Domain.DataTransferObjects.Legacy
{
    public partial class TblUserRank
    {
        public long Id { get; set; }
        public bool? IsEnabled { get; set; }
        public DateTime? CreatedOn { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? ModifiedOn { get; set; }
        public long? ModifiedBy { get; set; }
        public DateTime? LastChanged { get; set; }
        public long? UserAuthId { get; set; }
        public long? RankCd { get; set; }
        public int? RankSort { get; set; }
        public string RankName { get; set; }
        public long? RankConditionIntroduce { get; set; }
        public decimal? RankRateAffiliate { get; set; }
        public decimal? RankRateBinary { get; set; }
        public decimal? RankRateDaily { get; set; }

        public virtual TblUserAuth UserAuth { get; set; }
    }
}
