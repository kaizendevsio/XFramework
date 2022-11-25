using System;
using System.Collections.Generic;

namespace XFramework.Domain.DataTransferObjects;

public partial class IdentityCredential
{
    public long Id { get; set; }

    public bool IsEnabled { get; set; }

    public DateTime CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? ModifiedAt { get; set; }

    public long? ModifiedBy { get; set; }

    public bool IsDeleted { get; set; }

    public long? IdentityInfoId { get; set; }

    public string? UserName { get; set; }

    public string? UserAlias { get; set; }

    public short? LogInStatus { get; set; }

    public byte[]? PasswordByte { get; set; }

    public long ApplicationId { get; set; }

    public string? Token { get; set; }

    public string Guid { get; set; } = null!;

    public virtual Application Application { get; set; } = null!;

    public virtual ICollection<AuthorizationLog> AuthorizationLogs { get; } = new List<AuthorizationLog>();

    public virtual ICollection<DepositRequest> DepositRequests { get; } = new List<DepositRequest>();

    public virtual ICollection<IdentityContact> IdentityContacts { get; } = new List<IdentityContact>();

    public virtual ICollection<IdentityFavorite> IdentityFavorites { get; } = new List<IdentityFavorite>();

    public virtual IdentityInformation? IdentityInfo { get; set; }

    public virtual ICollection<IdentityRole> IdentityRoles { get; } = new List<IdentityRole>();

    public virtual ICollection<IdentityVerification> IdentityVerifications { get; } = new List<IdentityVerification>();

    public virtual ICollection<SessionDatum> SessionData { get; } = new List<SessionDatum>();

    public virtual ICollection<WalletTransaction> WalletTransactions { get; } = new List<WalletTransaction>();

    public virtual ICollection<Wallet> Wallets { get; } = new List<Wallet>();

    public virtual ICollection<WithdrawalRequest> WithdrawalRequests { get; } = new List<WithdrawalRequest>();
}
