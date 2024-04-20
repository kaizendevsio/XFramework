using XFramework.Domain.Shared.Contracts.Base;

namespace XFramework.Domain.Shared.Contracts;

public partial class IncomeType : BaseModel
{
    public string IncomeTypeName { get; set; } = null!;

    public string? IncomeTypeShortName { get; set; }

    public string? IncomeTypeDescription { get; set; }

    public bool? IsReward { get; set; }
    
    public virtual ICollection<IncomeTransaction> IncomeTransactions { get; set; } = new List<IncomeTransaction>();
}