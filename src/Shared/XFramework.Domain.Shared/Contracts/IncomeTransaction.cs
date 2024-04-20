using XFramework.Domain.Shared.Contracts.Base;

namespace XFramework.Domain.Shared.Contracts;

public partial class IncomeTransaction : BaseModel
{
    public Guid CredentialId { get; set; }

    public Guid? IncomeTypeId { get; set; }

    public decimal? IncomeValue { get; set; }

    public short? TransactionType { get; set; }

    public Guid SourceMapId { get; set; }

    public short? IncomeStatus { get; set; }

    public string? Remarks { get; set; }

    public Guid TargetMapId { get; set; }

    public Guid PairMapId { get; set; }


    public virtual IdentityCredential Credential { get; set; } = null!;

    public virtual IncomeType? IncomeType { get; set; }

}