using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace XFramework.Domain.Generic.Contracts;

public partial class IdentityCredential : BaseModel
{
    public Guid IdentityInfoId { get; set; }

    public string? UserName { get; set; }

    public string? UserAlias { get; set; }

    public short? LogInStatus { get; set; }

    [JsonIgnore]
    public byte[]? PasswordByte { get; set; }
    
    [NotMapped]
    public string? Password { get; set; }

    public Guid TenantId { get; set; }

    public string? Token { get; set; }


    public virtual Tenant Tenant { get; set; } = null!;

    public virtual ICollection<AuthorizationLog> AuthorizationLogs { get; } = new List<AuthorizationLog>();

    public virtual ICollection<BillsPaymentTransaction> BillsPaymentTransactions { get; } =
        new List<BillsPaymentTransaction>();

    public virtual ICollection<BusinessPackage> BusinessPackageConsumedByNavigations { get; } =
        new List<BusinessPackage>();

    public virtual ICollection<BusinessPackage> BusinessPackageIdentityCredentials { get; } =
        new List<BusinessPackage>();

    public virtual ICollection<BusinessPackage> BusinessPackageRecipientIdentityCredentials { get; } =
        new List<BusinessPackage>();

    public virtual ICollection<BusinessPackageUpgradeTransaction> BusinessPackageUpgradeTransactions { get; } =
        new List<BusinessPackageUpgradeTransaction>();

    public virtual ICollection<CommunityIdentity> CommunityIdentities { get; } = new List<CommunityIdentity>();

    public virtual ICollection<DepositRequest> DepositRequests { get; } = new List<DepositRequest>();

    public virtual ICollection<EloadTransaction> EloadTransactions { get; } = new List<EloadTransaction>();

    public virtual ICollection<IdentityContact> IdentityContacts { get; } = new List<IdentityContact>();

    public virtual ICollection<IdentityFavorite> IdentityFavorites { get; } = new List<IdentityFavorite>();

    public virtual IdentityInformation? IdentityInfo { get; set; }

    public virtual ICollection<IdentityRole> IdentityRoles { get; } = new List<IdentityRole>();

    public virtual ICollection<IdentityVerification> IdentityVerifications { get; } = new List<IdentityVerification>();

    public virtual ICollection<IncomeTransaction> IncomeTransactions { get; } = new List<IncomeTransaction>();

    public virtual ICollection<MessageDirect> MessageDirectRecipientNavigations { get; } = new List<MessageDirect>();

    public virtual ICollection<MessageDirect> MessageDirectSenderNavigations { get; } = new List<MessageDirect>();

    public virtual ICollection<MessageThreadMember> MessageThreadMembers { get; } = new List<MessageThreadMember>();

    public virtual ICollection<SessionDatum> SessionData { get; } = new List<SessionDatum>();

    public virtual ICollection<Subscription> Subscriptions { get; } = new List<Subscription>();

    public virtual ICollection<WalletTransaction> WalletTransactions { get; } = new List<WalletTransaction>();

    public virtual ICollection<Wallet> Wallets { get; } = new List<Wallet>();

    public virtual ICollection<WithdrawalRequest> WithdrawalRequests { get; } = new List<WithdrawalRequest>();
}