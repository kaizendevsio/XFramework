namespace XFramework.Domain.Generic.Contracts;

public partial class IncomeDistribution : BaseModel
{
    public Guid BusinessPackageId { get; set; }

    public Guid IncomeTypeId { get; set; }

    public decimal Value { get; set; }

    public long DistributionType { get; set; }

    public long? MaxLimit { get; set; }

    public long? MinLimit { get; set; }


    public virtual BusinessPackage BusinessPackage { get; set; } = null!;

    public virtual IncomeType IncomeType { get; set; } = null!;
}