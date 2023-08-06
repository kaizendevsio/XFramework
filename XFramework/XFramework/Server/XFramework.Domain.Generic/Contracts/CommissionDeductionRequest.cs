namespace XFramework.Domain.Generic.Contracts;

public partial class CommissionDeductionRequest
{
    public Guid Id { get; set; }

    public bool? IsDeleted { get; set; }

    public bool? IsEnabled { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public Guid? BusinessPackageId { get; set; }

    public decimal? PrincipalAmount { get; set; }

    public decimal? Balance { get; set; }

    public int? Status { get; set; }

    public decimal? DeductionCharge { get; set; }

    
    public virtual BusinessPackage? BusinessPackage { get; set; }
}
