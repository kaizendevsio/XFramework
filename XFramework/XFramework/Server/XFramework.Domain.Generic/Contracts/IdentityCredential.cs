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

    public string? Token { get; set; }


    public virtual Tenant Tenant { get; set; } = null!;

    public virtual ICollection<AuthorizationLog> AuthorizationLogs { get; set; } = new List<AuthorizationLog>();
    
    public virtual ICollection<BusinessPackage> BusinessPackageConsumedByNavigations { get; set; } =
        new List<BusinessPackage>();

    public virtual ICollection<BusinessPackage> BusinessPackageIdentityCredentials { get; set; } =
        new List<BusinessPackage>();

    public virtual ICollection<BusinessPackage> BusinessPackageRecipientIdentityCredentials { get; set; } =
        new List<BusinessPackage>();

    public virtual ICollection<BusinessPackageUpgradeTransaction> BusinessPackageUpgradeTransactions { get; set; } =
        new List<BusinessPackageUpgradeTransaction>();

    public virtual ICollection<CommunityIdentity> CommunityIdentities { get; set; } = new List<CommunityIdentity>();

    public virtual ICollection<DepositRequest> DepositRequests { get; set; } = new List<DepositRequest>();


    public virtual ICollection<IdentityContact> IdentityContacts { get; set; } = new List<IdentityContact>();

    public virtual ICollection<IdentityFavorite> IdentityFavorites { get; set; } = new List<IdentityFavorite>();

    public virtual IdentityInformation? IdentityInfo { get; set; }

    public virtual ICollection<IdentityRole> IdentityRoles { get; set; } = new List<IdentityRole>();

    public virtual ICollection<IdentityVerification> IdentityVerifications { get; set; } = new List<IdentityVerification>();

    public virtual ICollection<IncomeTransaction> IncomeTransactions { get; set; } = new List<IncomeTransaction>();

    public virtual ICollection<MessageDirect> MessageDirectRecipientNavigations { get; set; } = new List<MessageDirect>();

    public virtual ICollection<MessageDirect> MessageDirectSenderNavigations { get; set; } = new List<MessageDirect>();

    public virtual ICollection<MessageThreadMember> MessageThreadMembers { get; set; } = new List<MessageThreadMember>();

    public virtual ICollection<Session> SessionData { get; set; } = new List<Session>();

    public virtual ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();

    public virtual ICollection<WalletTransaction> WalletTransactions { get; set; } = new List<WalletTransaction>();

    public virtual ICollection<Wallet> Wallets { get; set; } = new List<Wallet>();

    public virtual ICollection<WithdrawalRequest> WithdrawalRequests { get; set; } = new List<WithdrawalRequest>();
}