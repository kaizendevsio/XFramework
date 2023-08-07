namespace XFramework.Domain.Generic.Contracts;

public partial class IncomePartition
{
    public Guid Id { get; set; }

    public bool? IsEnabled { get; set; }

    public DateTime CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime ModifiedAt { get; set; }

    public long? ModifiedBy { get; set; }

    public Guid IdentityRoleId { get; set; }

    public Guid? IncomeTypeId { get; set; }

    public decimal? Percentage { get; set; }

    
    public virtual IdentityRole? IdentityRole { get; set; }

    public virtual IncomeType? IncomeType { get; set; }
}
