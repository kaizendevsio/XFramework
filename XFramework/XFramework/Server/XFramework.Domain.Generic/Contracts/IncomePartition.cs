namespace XFramework.Domain.Generic.Contracts;

public partial class IncomePartition : BaseModel
{
    public Guid IdentityRoleId { get; set; }

    public Guid? IncomeTypeId { get; set; }

    public decimal? Percentage { get; set; }


    public virtual IdentityRole? IdentityRole { get; set; }

    public virtual IncomeType? IncomeType { get; set; }
}