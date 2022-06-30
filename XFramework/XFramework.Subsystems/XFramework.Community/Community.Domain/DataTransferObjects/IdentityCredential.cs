using System;
using System.Collections.Generic;

namespace Community.Domain.DataTransferObjects
{
    public partial class IdentityCredential
    {
        public IdentityCredential()
        {
            AuthorizationLogs = new HashSet<AuthorizationLog>();
            CommunityIdentities = new HashSet<CommunityIdentity>();
            IdentityContacts = new HashSet<IdentityContact>();
            IdentityFavorites = new HashSet<IdentityFavorite>();
            IdentityRoles = new HashSet<IdentityRole>();
            IdentityVerifications = new HashSet<IdentityVerification>();
            SessionData = new HashSet<SessionDatum>();
        }

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
        public virtual IdentityInformation? IdentityInfo { get; set; }
        public virtual ICollection<AuthorizationLog> AuthorizationLogs { get; set; }
        public virtual ICollection<CommunityIdentity> CommunityIdentities { get; set; }
        public virtual ICollection<IdentityContact> IdentityContacts { get; set; }
        public virtual ICollection<IdentityFavorite> IdentityFavorites { get; set; }
        public virtual ICollection<IdentityRole> IdentityRoles { get; set; }
        public virtual ICollection<IdentityVerification> IdentityVerifications { get; set; }
        public virtual ICollection<SessionDatum> SessionData { get; set; }
    }
}
