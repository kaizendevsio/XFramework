using XFramework.Domain.Shared.Attributes;
using XFramework.Domain.Shared.Enums;

namespace Wallets.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
[GenerateEndpoints(
    Type = EndpointType.Both,
    Actions = EndpointActions.All,
    RoutePrefix = "api/withdrawal-requests",
    RequireAuthorization = true,
    CacheDurationSeconds = 300,
    CacheKeyPrefix = "withdrawal-requests"
)]
public partial class WithdrawalRequest : BaseModel
{
    
    [MemoryPackOrder(0)]
    public Guid CredentialId { get; set; }

    [MemoryPackOrder(1)]
    public string? Address { get; set; }

    [MemoryPackOrder(2)]
    public decimal? Amount { get; set; }
    
    [MemoryPackOrder(3)]
    public decimal? Fee { get; set; }

    [MemoryPackOrder(4)]
    public TransactionStatus WithdrawalStatus { get; set; }

    [MemoryPackOrder(5)]
    public string? Remarks { get; set; }
    
    [MemoryPackOrder(6)]
    public string? ReferenceNumber { get; set; }

    [MemoryPackOrder(7)]
    public Guid WalletId { get; set; }

    [MemoryPackOrder(8)]
    public virtual IdentityCredential Credential { get; set; } = null!;

    [MemoryPackOrder(9)]
    public virtual Wallet? Wallet { get; set; }

}

public class CreateWithdrawalRequestRequest
{
    public Guid CredentialId { get; set; }
    public string? Address { get; set; }
    public decimal? Amount { get; set; }
    public decimal? Fee { get; set; }
    public TransactionStatus WithdrawalStatus { get; set; }
    public string? Remarks { get; set; }
    public string? ReferenceNumber { get; set; }
    public Guid WalletId { get; set; }
}

public class UpdateWithdrawalRequestRequest
{
    public string? Address { get; set; }
    public decimal? Amount { get; set; }
    public decimal? Fee { get; set; }
    public TransactionStatus WithdrawalStatus { get; set; }
    public string? Remarks { get; set; }
    public string? ReferenceNumber { get; set; }
}

public class GetWithdrawalRequestListRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SearchTerm { get; set; }
    public Guid? CredentialId { get; set; }
    public Guid? WalletId { get; set; }
    public TransactionStatus? WithdrawalStatus { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}
