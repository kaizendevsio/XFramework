using System;
using XFramework.Domain.Generic.Enums;

namespace XFramework.Domain.Generic.Contracts.Requests
{
    public class CreateUserRequest : RequestBase
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public DateTime Dob { get; set; }
        public Gender Gender { get; set; }
        public CivilStatus CivilStatus { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }

        public string UserAlias { get; set; }
        public string UserName { get; set; }
        public string PasswordString { get; set; }
        public string ConfirmPasswordString { get; set; }
        public int RoleEntity { get; set; }
        
        public string Uid { get; set; }
        public string Cuid { get; set; }
        
        public long PhoneCid { get; set; }
        public long EmailCid { get; set; }
    }
}