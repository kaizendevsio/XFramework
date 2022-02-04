using System;
using System.Collections.Generic;

namespace IdentityServer.Domain.DataTransferObjects.Legacy;

public partial class TblUserInfo
{
    public TblUserInfo()
    {
        TblAuditSystemLogs = new HashSet<TblAuditSystemLogs>();
        TblUserAddresses = new HashSet<TblUserAddresses>();
        TblUserAuth = new HashSet<TblUserAuth>();
    }

    public long Id { get; set; }
    public bool? IsEnabled { get; set; }
    public DateTime? CreatedOn { get; set; }
    public long? CreatedBy { get; set; }
    public DateTime? ModifiedOn { get; set; }
    public long? ModifiedBy { get; set; }
    public DateTime? LastChanged { get; set; }
    public string Uid { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public DateTime? Dob { get; set; }
    public string Email { get; set; }
    public string CompanyName { get; set; }
    public string PhoneNumber { get; set; }
    public short? Gender { get; set; }
    public string CountryIsoCode2 { get; set; }
    public string ConfirmedEmail { get; set; }
    public byte[] VerifyEmailCodeByte { get; set; }
    public short? EmailStatus { get; set; }
    public int? OtpStatus { get; set; }
    public bool? IsExistingUser { get; set; }

    public virtual ICollection<TblAuditSystemLogs> TblAuditSystemLogs { get; set; }
    public virtual ICollection<TblUserAddresses> TblUserAddresses { get; set; }
    public virtual ICollection<TblUserAuth> TblUserAuth { get; set; }
}