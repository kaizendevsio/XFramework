using System;
using System.Collections.Generic;

#nullable disable

namespace PaymentGateways.Domain.DataTransferObjects
{
    public partial class TblIdentityCredential
    {
        public TblIdentityCredential()
        {
            TblAuthorizationLogs = new HashSet<TblAuthorizationLog>();
            TblIdentityContacts = new HashSet<TblIdentityContact>();
            TblIdentityRoles = new HashSet<TblIdentityRole>();
            TblIdentityVerifications = new HashSet<TblIdentityVerification>();
            TblSessionData = new HashSet<TblSessionDatum>();
            TblUserBillsPaymentTransactions = new HashSet<TblUserBillsPaymentTransaction>();
            TblUserBusinessPackageConsumedByNavigations = new HashSet<TblUserBusinessPackage>();
            TblUserBusinessPackageRecipientAuths = new HashSet<TblUserBusinessPackage>();
            TblUserBusinessPackageUserAuths = new HashSet<TblUserBusinessPackage>();
            TblUserDepositRequests = new HashSet<TblUserDepositRequest>();
            TblUserIncomeTransactions = new HashSet<TblUserIncomeTransaction>();
            TblUserMaps = new HashSet<TblUserMap>();
            TblUserWalletAddresses = new HashSet<TblUserWalletAddress>();
            TblUserWalletTransactions = new HashSet<TblUserWalletTransaction>();
            TblUserWallets = new HashSet<TblUserWallet>();
            TblUserWithdrawalRequests = new HashSet<TblUserWithdrawalRequest>();
        }

        public long Id { get; set; }
        public bool IsEnabled { get; set; }
        public DateTime CreatedAt { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public long? ModifiedBy { get; set; }
        public bool IsDeleted { get; set; }
        public long? IdentityInfoId { get; set; }
        public string UserName { get; set; }
        public string UserAlias { get; set; }
        public short? LogInStatus { get; set; }
        public byte[] PasswordByte { get; set; }
        public long ApplicationId { get; set; }
        public string Token { get; set; }
        public string Cuid { get; set; }

        public virtual TblApplication Application { get; set; }
        public virtual TblIdentityInformation IdentityInfo { get; set; }
        public virtual ICollection<TblAuthorizationLog> TblAuthorizationLogs { get; set; }
        public virtual ICollection<TblIdentityContact> TblIdentityContacts { get; set; }
        public virtual ICollection<TblIdentityRole> TblIdentityRoles { get; set; }
        public virtual ICollection<TblIdentityVerification> TblIdentityVerifications { get; set; }
        public virtual ICollection<TblSessionDatum> TblSessionData { get; set; }
        public virtual ICollection<TblUserBillsPaymentTransaction> TblUserBillsPaymentTransactions { get; set; }
        public virtual ICollection<TblUserBusinessPackage> TblUserBusinessPackageConsumedByNavigations { get; set; }
        public virtual ICollection<TblUserBusinessPackage> TblUserBusinessPackageRecipientAuths { get; set; }
        public virtual ICollection<TblUserBusinessPackage> TblUserBusinessPackageUserAuths { get; set; }
        public virtual ICollection<TblUserDepositRequest> TblUserDepositRequests { get; set; }
        public virtual ICollection<TblUserIncomeTransaction> TblUserIncomeTransactions { get; set; }
        public virtual ICollection<TblUserMap> TblUserMaps { get; set; }
        public virtual ICollection<TblUserWalletAddress> TblUserWalletAddresses { get; set; }
        public virtual ICollection<TblUserWalletTransaction> TblUserWalletTransactions { get; set; }
        public virtual ICollection<TblUserWallet> TblUserWallets { get; set; }
        public virtual ICollection<TblUserWithdrawalRequest> TblUserWithdrawalRequests { get; set; }
    }
}
