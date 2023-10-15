namespace XFramework.Domain.Generic.Contracts;

public partial record WalletAddress : BaseModel
{
    public string? Address { get; set; }

    public decimal? Balance { get; set; }

    public string? Remarks { get; set; }


    public Guid WalletId { get; set; }

    public virtual Wallet Wallet { get; set; } = null!;
}