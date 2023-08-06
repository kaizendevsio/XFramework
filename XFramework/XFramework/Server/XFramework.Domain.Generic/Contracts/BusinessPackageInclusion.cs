namespace XFramework.Domain.Generic.Contracts;

public partial class BusinessPackageInclusion
{
    public Guid Id { get; set; }

    public bool? IsEnabled { get; set; }

    public DateTime? CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? ModifiedAt { get; set; }

    public long? ModifiedBy { get; set; }

    public DateTime? LastChanged { get; set; }

    public Guid? BusinessPackageId { get; set; }

    public Guid? InclusionTypeId { get; set; }

    public decimal? Value { get; set; }

    public string? StringValue { get; set; }

    
    public virtual BusinessPackageInclusionType? InclusionType { get; set; }
}
