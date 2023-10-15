namespace XFramework.Domain.Generic.Contracts;

public partial record CommissionDeductionRequest : BaseModel
{
    public Guid? BusinessPackageId { get; set; }

    public decimal? PrincipalAmount { get; set; }

    public decimal? Balance { get; set; }

    public int? Status { get; set; }

    public decimal? DeductionCharge { get; set; }


    public virtual BusinessPackage? BusinessPackage { get; set; }
}