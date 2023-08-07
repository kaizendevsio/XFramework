namespace XFramework.Domain.Generic.Contracts;

public partial class WalletAddress
{
    public Guid Id { get; set; }

    public bool? IsEnabled { get; set; }

    public DateTime CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime ModifiedAt { get; set; }

    public long? ModifiedBy { get; set; }

    public string? Address { get; set; }

    public decimal? Balance { get; set; }

    public string? Remarks { get; set; }

    
    public Guid WalletId { get; set; }

    public virtual Wallet Wallet { get; set; } = null!;
}
