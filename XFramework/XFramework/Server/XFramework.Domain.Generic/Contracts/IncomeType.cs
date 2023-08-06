namespace XFramework.Domain.Generic.Contracts;

public partial class IncomeType
{
    public Guid Id { get; set; }

    public bool? IsEnabled { get; set; }

    public DateTime CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? ModifiedAt { get; set; }

    public long? ModifiedBy { get; set; }

    public string IncomeTypeName { get; set; } = null!;

    public string? IncomeTypeShortName { get; set; }

    public string? IncomeTypeDescription { get; set; }

    public bool? IsReward { get; set; }

    
    public virtual ICollection<IncomeDistribution> IncomeDistributions { get; } = new List<IncomeDistribution>();

    public virtual ICollection<IncomePartition> IncomePartitions { get; } = new List<IncomePartition>();

    public virtual ICollection<IncomeTransaction> IncomeTransactions { get; } = new List<IncomeTransaction>();
}
