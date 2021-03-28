using System;
using System.ComponentModel.DataAnnotations;
using XFramework.Domain.Enums;

namespace XFramework.Domain.BusinessObjects
{
   public class UserBO : BaseAuditBO
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public DateTime? Dob { get; set; }
        public int Age { get; set; }
        public string Email { get; set; }
        public string CompanyName { get; set; }
        public string PhoneNumber { get; set; }
        public short? Gender { get; set; }
        public string CountryIsoCode2 { get; set; }
        [Required]
        public string PasswordString { get; set; }
        [Required]
        public string UserName { get; set; }
        public bool? IsTempPassword { get; set; }
        public string Uid { get; set; }
        public bool IsVerified { get; set; }
        public short CivilStatus { get; set; }
        public DateTime Expiration { get; set; }
        public UserRole Role { get; set; }
    }
}
