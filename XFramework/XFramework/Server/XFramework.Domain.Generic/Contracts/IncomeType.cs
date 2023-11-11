namespace XFramework.Domain.Generic.Contracts;

public partial class IncomeType : BaseModel
{
    public string IncomeTypeName { get; set; } = null!;

    public string? IncomeTypeShortName { get; set; }

    public string? IncomeTypeDescription { get; set; }

    public bool? IsReward { get; set; }


    public virtual ICollection<IncomeDistribution> IncomeDistributions { get; } = new List<IncomeDistribution>();

    public virtual ICollection<IncomePartition> IncomePartitions { get; } = new List<IncomePartition>();

    public virtual ICollection<IncomeTransaction> IncomeTransactions { get; } = new List<IncomeTransaction>();
}