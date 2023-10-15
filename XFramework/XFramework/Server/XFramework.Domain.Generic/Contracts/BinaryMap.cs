namespace XFramework.Domain.Generic.Contracts;

public partial record BinaryMap : BaseModel
{
    public Guid? SponsorUserId { get; set; }

    public Guid? UplineUserId { get; set; }

    public short? Position { get; set; }

    public long LeftLegCount { get; set; }

    public long RightLegCount { get; set; }

    public string? Alias { get; set; }

    public long? Level { get; set; }

    public long? BinaryLeft { get; set; }

    public long? BinaryRight { get; set; }

    public int? LeftLegSilverCount { get; set; }

    public int? RightLegSilverCount { get; set; }

    public int? LeftLegGoldCount { get; set; }

    public int? RightLegGoldCount { get; set; }

    public int? UplineLevel { get; set; }

    public virtual ICollection<BinaryListMultiplex> BinaryListMultiplexes { get; } = new List<BinaryListMultiplex>();

    public virtual ICollection<BinaryList> BinaryListSourceUserMaps { get; } = new List<BinaryList>();

    public virtual ICollection<BinaryList> BinaryListTargetUserMaps { get; } = new List<BinaryList>();

    public virtual BusinessPackage IdNavigation { get; set; } = null!;

    public virtual ICollection<IncomeTransaction> IncomeTransactionPairMaps { get; } = new List<IncomeTransaction>();

    public virtual ICollection<IncomeTransaction> IncomeTransactionSourceMaps { get; } = new List<IncomeTransaction>();

    public virtual ICollection<IncomeTransaction> IncomeTransactionTargetMaps { get; } = new List<IncomeTransaction>();

    public virtual ICollection<BinaryMap> InverseSponsorUser { get; } = new List<BinaryMap>();

    public virtual ICollection<BinaryMap> InverseUplineUser { get; } = new List<BinaryMap>();

    public virtual BinaryMap? SponsorUser { get; set; }

    public virtual BinaryMap? UplineUser { get; set; }
}